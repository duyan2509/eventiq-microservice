using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.SeatService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNextSeatNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "next_seat_number",
                schema: "seat_service",
                table: "seat_maps",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "next_seat_number",
                schema: "seat_service",
                table: "seat_maps");
        }
    }
}
