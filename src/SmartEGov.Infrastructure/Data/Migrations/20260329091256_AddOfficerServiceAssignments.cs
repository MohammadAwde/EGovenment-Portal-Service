using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEGov.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficerServiceAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OfficerServiceAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfficerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GovernmentServiceId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficerServiceAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfficerServiceAssignments_AspNetUsers_OfficerId",
                        column: x => x.OfficerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OfficerServiceAssignments_GovernmentServices_GovernmentServiceId",
                        column: x => x.GovernmentServiceId,
                        principalTable: "GovernmentServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfficerServiceAssignments_GovernmentServiceId",
                table: "OfficerServiceAssignments",
                column: "GovernmentServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficerServiceAssignments_OfficerId_GovernmentServiceId",
                table: "OfficerServiceAssignments",
                columns: new[] { "OfficerId", "GovernmentServiceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfficerServiceAssignments");
        }
    }
}
