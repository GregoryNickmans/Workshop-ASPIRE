using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WorkshopHub.Infrastructure.Persistence.Migrations.SqlServer;

// Historical migration identity and schema preserved after moving persistence to Infrastructure.
[DbContext(typeof(DatabaseContext))]
[Migration("202611070001_InitialBookings")]
public sealed class InitialBookings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("Bookings", columns: table => new
        {
            Id = table.Column<Guid>(nullable: false),
            Workshop = table.Column<string>(maxLength: 80, nullable: false),
            Status = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Pending"),
            CreatedAt = table.Column<DateTimeOffset>(nullable: false),
            ConfirmedAt = table.Column<DateTimeOffset>(nullable: true),
            NotificationPublishedAt = table.Column<DateTimeOffset>(nullable: true),
            AttachmentBlobName = table.Column<string>(maxLength: 100, nullable: true),
            AttachmentFileName = table.Column<string>(maxLength: 120, nullable: true),
            AttachmentContentType = table.Column<string>(maxLength: 100, nullable: true),
            AttachmentLength = table.Column<long>(nullable: true)
        }, constraints: table => table.PrimaryKey("PK_Bookings", x => x.Id));
        migrationBuilder.CreateIndex("IX_Bookings_CreatedAt", "Bookings", "CreatedAt");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("Bookings");
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.10");
        // String-based historical model avoids applying the new aggregate's soft-delete property to old history.
        modelBuilder.Entity("WorkshopHub.Domain.Booking", booking =>
        {
            booking.HasKey("Id");
            booking.Property<Guid>("Id");
            booking.Property<string>("Workshop").HasMaxLength(80).IsRequired();
            booking.Property<string>("Status").HasMaxLength(20).HasDefaultValue("Pending").IsRequired();
            booking.Property<DateTimeOffset>("CreatedAt").IsRequired();
            booking.Property<DateTimeOffset?>("ConfirmedAt");
            booking.Property<DateTimeOffset?>("NotificationPublishedAt");
            booking.Property<string>("AttachmentBlobName").HasMaxLength(100);
            booking.Property<string>("AttachmentFileName").HasMaxLength(120);
            booking.Property<string>("AttachmentContentType").HasMaxLength(100);
            booking.Property<long?>("AttachmentLength");
            booking.HasIndex("CreatedAt");
            booking.ToTable("Bookings");
        });
    }
}
