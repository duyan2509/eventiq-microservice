using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.SeatService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeatPositionColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_seats_seat_map_id",
                schema: "seat_service",
                table: "seats");

            migrationBuilder.AddColumn<double>(
                name: "position_x",
                schema: "seat_service",
                table: "seats",
                type: "double precision",
                nullable: true,
                computedColumnSql: "((position ->> 'x'))::double precision",
                stored: true);

            migrationBuilder.AddColumn<double>(
                name: "position_y",
                schema: "seat_service",
                table: "seats",
                type: "double precision",
                nullable: true,
                computedColumnSql: "((position ->> 'y'))::double precision",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_seats_map_position",
                schema: "seat_service",
                table: "seats",
                columns: new[] { "seat_map_id", "position_x", "position_y" },
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_seats_map_position",
                schema: "seat_service",
                table: "seats");

            migrationBuilder.DropColumn(
                name: "position_x",
                schema: "seat_service",
                table: "seats");

            migrationBuilder.DropColumn(
                name: "position_y",
                schema: "seat_service",
                table: "seats");

            migrationBuilder.CreateIndex(
                name: "ix_seats_seat_map_id",
                schema: "seat_service",
                table: "seats",
                column: "seat_map_id");
        }
    }
}
