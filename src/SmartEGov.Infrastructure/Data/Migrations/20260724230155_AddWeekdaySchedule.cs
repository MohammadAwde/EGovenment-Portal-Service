using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEGov.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWeekdaySchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeekdaySchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    OpenTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    CloseTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    BreakStart = table.Column<TimeSpan>(type: "time", nullable: false),
                    BreakEnd = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeekdaySchedules", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeekdaySchedules");
        }
    }
}
