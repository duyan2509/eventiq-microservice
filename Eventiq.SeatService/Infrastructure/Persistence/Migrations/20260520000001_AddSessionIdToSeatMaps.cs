using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.SeatService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionIdToSeatMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "session_id",
                schema: "seat_service",
                table: "seat_maps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "total_seats",
                schema: "seat_service",
                table: "seat_maps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Drop the old blanket unique index on chart_id
            migrationBuilder.DropIndex(
                name: "ix_seat_maps_chart_id",
                schema: "seat_service",
                table: "seat_maps");

            // Template: one seat map per chart when no session is linked
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ix_seat_maps_chart_id_template ON seat_service.seat_maps (chart_id) WHERE session_id IS NULL AND is_deleted = false;");

            // Session clone: one seat map per (chart, session)
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ix_seat_maps_chart_session ON seat_service.seat_maps (chart_id, session_id) WHERE session_id IS NOT NULL AND is_deleted = false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS seat_service.ix_seat_maps_chart_id_template;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS seat_service.ix_seat_maps_chart_session;");

            migrationBuilder.DropColumn(
                name: "session_id",
                schema: "seat_service",
                table: "seat_maps");

            migrationBuilder.DropColumn(
                name: "total_seats",
                schema: "seat_service",
                table: "seat_maps");

            migrationBuilder.CreateIndex(
                name: "ix_seat_maps_chart_id",
                schema: "seat_service",
                table: "seat_maps",
                column: "chart_id",
                unique: true);
        }
    }
}
