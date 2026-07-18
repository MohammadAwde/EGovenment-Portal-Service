using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEGov.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQueueNumberToAppointment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QueueNumber",
                table: "Appointments",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QueueNumber",
                table: "Appointments");
        }
    }
}
