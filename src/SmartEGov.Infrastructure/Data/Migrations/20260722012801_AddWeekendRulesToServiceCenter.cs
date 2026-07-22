using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEGov.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWeekendRulesToServiceCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "FridayClosingOverride",
                table: "ServiceCenters",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WorksOnSaturday",
                table: "ServiceCenters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WorksOnSunday",
                table: "ServiceCenters",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FridayClosingOverride",
                table: "ServiceCenters");

            migrationBuilder.DropColumn(
                name: "WorksOnSaturday",
                table: "ServiceCenters");

            migrationBuilder.DropColumn(
                name: "WorksOnSunday",
                table: "ServiceCenters");
        }
    }
}
