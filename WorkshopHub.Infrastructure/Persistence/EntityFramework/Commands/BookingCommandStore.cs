using System.Data;
using ErrorOr;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkshopHub.Application.Abstractions;
using WorkshopHub.Application.Models;
using WorkshopHub.Domain.Entities;
using WorkshopHub.Domain.ValueObjects;
using WorkshopHub.Infrastructure.Persistence.EntityFramework.Queries;

namespace WorkshopHub.Infrastructure.Persistence.EntityFramework.Commands;

public sealed class BookingCommandStore(DatabaseContext database, TimeProvider timeProvider) : IBookingCommandStore
{
    public Task<ErrorOr<BookingView>> CreateBookingAsync(CourseId courseId, string? name, string? email, CancellationToken cancellationToken) =>
        ExecuteAsync<BookingView>(async token =>
        {
            var course = await database.Courses.SingleOrDefaultAsync(x => x.Id == courseId, token);
            if (course is null) { return CourseNotFound(); }
            var booking = Booking.Create(course, name, email, timeProvider);
            if (booking.IsError) { return booking.Errors; }
            var reserved = await database.Courses.Where(x => x.Id == courseId && !x.IsArchived && x.OccupiedSeats < x.MaximumParticipants)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.OccupiedSeats, x => x.OccupiedSeats + 1), token);
            if (reserved != 1) { return Error.Conflict("Course.Full", "The course is full or archived. Refresh availability."); }
            database.Bookings.Add(booking.Value);
            await database.SaveChangesAsync(token);
            return BookingProjection.Map(booking.Value);
        }, cancellationToken);

    public Task<ErrorOr<BookingView>> TransitionAsync(BookingId bookingId, bool cancel, CancellationToken cancellationToken) =>
        ExecuteAsync<BookingView>(async token =>
        {
            var booking = await database.Bookings.SingleOrDefaultAsync(x => x.Id == bookingId, token);
            if (booking is null) { return Error.NotFound("Booking.NotFound", "Booking was not found."); }
            var previousStatus = booking.Status;
            var result = cancel ? booking.Cancel(timeProvider) : booking.Confirm(timeProvider);
            if (result.IsError) { return result.Errors; }
            if (previousStatus == booking.Status) { return BookingProjection.Map(booking); }
            // Conditional transitions also protect against lost updates if another provider uses deferred transactions.
            var changed = await database.Bookings.Where(x => x.Id == bookingId && x.Status == previousStatus)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, booking.Status)
                    .SetProperty(x => x.ConfirmedAt, booking.ConfirmedAt).SetProperty(x => x.CancelledAt, booking.CancelledAt), token);
            if (changed != 1) { return Error.Conflict("Booking.Changed", "Booking changed. Reload its persisted state."); }
            if (cancel && booking.CourseId is not null)
            {
                var released = await database.Courses.Where(x => x.Id == booking.CourseId && x.OccupiedSeats > 0)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.OccupiedSeats, x => x.OccupiedSeats - 1), token);
                if (released != 1) { return Error.Conflict("Course.Inventory", "Inventory is inconsistent. No changes were saved."); }
            }
            return BookingProjection.Map(booking);
        }, cancellationToken);

    public Task<ErrorOr<CourseView>> CreateCourseAsync(Course course, CancellationToken cancellationToken) =>
        ExecuteAsync<CourseView>(async token =>
        {
            database.Courses.Add(course);
            await database.SaveChangesAsync(token);
            return BookingProjection.Map(course);
        }, cancellationToken);

    public Task<ErrorOr<CourseView>> UpdateCourseAsync(CourseId courseId, string? title, string? description, decimal price,
        int capacity, int revision, CancellationToken cancellationToken) => ExecuteAsync<CourseView>(async token =>
        {
            var course = await database.Courses.SingleOrDefaultAsync(x => x.Id == courseId, token);
            if (course is null) { return CourseNotFound(); }
            var result = course.Update(title, description, price, capacity, revision);
            if (result.IsError) { return result.Errors; }
            await database.SaveChangesAsync(token);
            return BookingProjection.Map(course);
        }, cancellationToken);

    public Task<ErrorOr<CourseView>> ArchiveCourseAsync(CourseId courseId, int revision, CancellationToken cancellationToken) =>
        ExecuteAsync<CourseView>(async token =>
        {
            var course = await database.Courses.SingleOrDefaultAsync(x => x.Id == courseId, token);
            if (course is null) { return CourseNotFound(); }
            var result = course.Archive(revision);
            if (result.IsError) { return result.Errors; }
            await database.SaveChangesAsync(token);
            return BookingProjection.Map(course);
        }, cancellationToken);

    private async Task<ErrorOr<T>> ExecuteAsync<T>(Func<CancellationToken, Task<ErrorOr<T>>> command, CancellationToken cancellationToken)
    {
        // SQLite's non-deferred transaction acquires the writer lock before reading inventory. Retry the entire
        // transaction only for known BUSY/LOCKED errors. This never repeats an already-committed HTTP operation.
        for (var attempt = 0; ; attempt++)
        {
            database.ChangeTracker.Clear();
            try
            {
                await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var result = await command(cancellationToken);
                if (result.IsError)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return result.Errors;
                }
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception error) when (FindSqlite(error) is { SqliteErrorCode: 5 or 6 })
            {
                if (attempt >= 2) { return Error.Conflict("Storage.Busy", "The database is busy. Check persisted state before retrying."); }
                await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), cancellationToken);
            }
            catch (DbUpdateException error) when (FindSqlite(error) is { SqliteExtendedErrorCode: 2067 })
            {
                return Error.Conflict("Booking.Duplicate", "This email already has an active booking for this course.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return Error.Conflict("Course.Revision", "This record changed. Reload it before editing.");
            }
            finally
            {
                database.ChangeTracker.Clear();
            }
        }
    }

    private static SqliteException? FindSqlite(Exception? error) => error switch
    {
        SqliteException sqlite => sqlite,
        { InnerException: not null } => FindSqlite(error.InnerException),
        _ => null
    };
    private static Error CourseNotFound() => Error.NotFound("Course.NotFound", "Course was not found.");
}
