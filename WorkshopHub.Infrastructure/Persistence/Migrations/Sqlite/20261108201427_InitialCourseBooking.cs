using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WorkshopHub.Infrastructure.Persistence.SqliteMigrations;

/// <inheritdoc />
public partial class InitialCourseBooking : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Courses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                ShortDescription = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                PriceCents = table.Column<int>(type: "INTEGER", nullable: false),
                MaximumParticipants = table.Column<int>(type: "INTEGER", nullable: false),
                OccupiedSeats = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                IsArchived = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                Revision = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Courses", x => x.Id);
                table.CheckConstraint("CK_Courses_Price", "[PriceCents] >= 0");
                table.CheckConstraint("CK_Courses_Seats", "[MaximumParticipants] > 0 AND [OccupiedSeats] >= 0 AND [OccupiedSeats] <= [MaximumParticipants]");
            });

        migrationBuilder.CreateTable(
            name: "Bookings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CourseId = table.Column<Guid>(type: "TEXT", nullable: true),
                PriceCents = table.Column<int>(type: "INTEGER", nullable: true),
                ParticipantName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                ParticipantEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                CancelledAt = table.Column<long>(type: "INTEGER", nullable: true),
                Workshop = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Pending"),
                CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                ConfirmedAt = table.Column<long>(type: "INTEGER", nullable: true),
                NotificationPublishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                AttachmentBlobName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                AttachmentFileName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                AttachmentContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                AttachmentLength = table.Column<long>(type: "INTEGER", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Bookings", x => x.Id);
                table.CheckConstraint("CK_Bookings_Contact", "[CourseId] IS NULL OR ([ParticipantName] IS NOT NULL AND [ParticipantEmail] IS NOT NULL AND [PriceCents] IS NOT NULL)");
                table.CheckConstraint("CK_Bookings_Status", "[Status] IN ('Pending', 'Confirmed', 'Cancelled')");
                table.ForeignKey(
                    name: "FK_Bookings_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.InsertData(
            table: "Courses",
            columns: new[] { "Id", "MaximumParticipants", "PriceCents", "ShortDescription", "Title" },
            values: new object[,]
            {
                { new Guid("b5274a15-4cac-4fd8-9c5c-ff5252a6cf01"), 12, 7900, "Build a fresh perspective with hands-on exercises in ideation, collaboration and creative problem solving. Sample course.", "Creative thinking, practical tools" },
                { new Guid("b5274a15-4cac-4fd8-9c5c-ff5252a6cf02"), 8, 12500, "Explore the principles behind thoughtful digital experiences, from first sketch to an accessible prototype. Sample course.", "Design for the everyday" },
                { new Guid("b5274a15-4cac-4fd8-9c5c-ff5252a6cf03"), 10, 14900, "Turn an idea into a working web app. A friendly introduction to the building blocks of modern development. Sample course.", "Your first web application" },
                { new Guid("b5274a15-4cac-4fd8-9c5c-ff5252a6cf04"), 16, 6500, "Find your voice and shape a story that connects. Practice clear, confident communication in a supportive workshop. Sample course.", "Present with confidence" }
            });

        migrationBuilder.CreateIndex(
            name: "IX_Bookings_CourseId_ParticipantEmail",
            table: "Bookings",
            columns: new[] { "CourseId", "ParticipantEmail" },
            unique: true,
            filter: "[CourseId] IS NOT NULL AND [IsDeleted] = 0 AND [Status] IN ('Pending', 'Confirmed')");

        migrationBuilder.CreateIndex(
            name: "IX_Bookings_CreatedAt",
            table: "Bookings",
            column: "CreatedAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Bookings");

        migrationBuilder.DropTable(
            name: "Courses");
    }
}
