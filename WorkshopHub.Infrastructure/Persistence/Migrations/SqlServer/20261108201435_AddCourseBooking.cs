using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WorkshopHub.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCourseBooking : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CancelledAt",
            table: "Bookings",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CourseId",
            table: "Bookings",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ParticipantEmail",
            table: "Bookings",
            type: "nvarchar(254)",
            maxLength: 254,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ParticipantName",
            table: "Bookings",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PriceCents",
            table: "Bookings",
            type: "int",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Courses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                ShortDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                PriceCents = table.Column<int>(type: "int", nullable: false),
                MaximumParticipants = table.Column<int>(type: "int", nullable: false),
                OccupiedSeats = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                Revision = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Courses", x => x.Id);
                table.CheckConstraint("CK_Courses_Price", "[PriceCents] >= 0");
                table.CheckConstraint("CK_Courses_Seats", "[MaximumParticipants] > 0 AND [OccupiedSeats] >= 0 AND [OccupiedSeats] <= [MaximumParticipants]");
            });

        migrationBuilder.CreateIndex(
            name: "IX_Bookings_CourseId_ParticipantEmail",
            table: "Bookings",
            columns: new[] { "CourseId", "ParticipantEmail" },
            unique: true,
            filter: "[CourseId] IS NOT NULL AND [IsDeleted] = 0 AND [Status] IN ('Pending', 'Confirmed')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Bookings_Contact",
            table: "Bookings",
            sql: "[CourseId] IS NULL OR ([ParticipantName] IS NOT NULL AND [ParticipantEmail] IS NOT NULL AND [PriceCents] IS NOT NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Bookings_Status",
            table: "Bookings",
            sql: "[Status] IN ('Pending', 'Confirmed', 'Cancelled')");

        migrationBuilder.AddForeignKey(
            name: "FK_Bookings_Courses_CourseId",
            table: "Bookings",
            column: "CourseId",
            principalTable: "Courses",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Bookings_Courses_CourseId",
            table: "Bookings");

        migrationBuilder.DropTable(
            name: "Courses");

        migrationBuilder.DropIndex(
            name: "IX_Bookings_CourseId_ParticipantEmail",
            table: "Bookings");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Bookings_Contact",
            table: "Bookings");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Bookings_Status",
            table: "Bookings");

        migrationBuilder.DropColumn(
            name: "CancelledAt",
            table: "Bookings");

        migrationBuilder.DropColumn(
            name: "CourseId",
            table: "Bookings");

        migrationBuilder.DropColumn(
            name: "ParticipantEmail",
            table: "Bookings");

        migrationBuilder.DropColumn(
            name: "ParticipantName",
            table: "Bookings");

        migrationBuilder.DropColumn(
            name: "PriceCents",
            table: "Bookings");
    }
}
