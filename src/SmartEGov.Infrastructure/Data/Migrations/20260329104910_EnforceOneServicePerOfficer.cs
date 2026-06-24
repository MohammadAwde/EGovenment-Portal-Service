using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEGov.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceOneServicePerOfficer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OfficerServiceAssignments_OfficerId_GovernmentServiceId",
                table: "OfficerServiceAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_OfficerServiceAssignments_OfficerId",
                table: "OfficerServiceAssignments",
                column: "OfficerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OfficerServiceAssignments_OfficerId",
                table: "OfficerServiceAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_OfficerServiceAssignments_OfficerId_GovernmentServiceId",
                table: "OfficerServiceAssignments",
                columns: new[] { "OfficerId", "GovernmentServiceId" },
                unique: true);
        }
    }
}
