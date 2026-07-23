using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEGov.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportReplies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupportMessageId = table.Column<int>(type: "int", nullable: false),
                    SenderUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsFromAdmin = table.Column<bool>(type: "bit", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportReplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportReplies_SupportMessages_SupportMessageId",
                        column: x => x.SupportMessageId,
                        principalTable: "SupportMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportReplies_SupportMessageId",
                table: "SupportReplies",
                column: "SupportMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportReplies");
        }
    }
}
