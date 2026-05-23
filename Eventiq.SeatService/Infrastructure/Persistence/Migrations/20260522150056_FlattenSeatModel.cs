using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.SeatService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FlattenSeatModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add new column (nullable so backfill can run first)
            migrationBuilder.AddColumn<Guid>(
                name: "seat_map_id",
                schema: "seat_service",
                table: "seats",
                type: "uuid",
                nullable: true);

            // 2. Backfill from row → section → seat_map join (rows/sections still exist)
            migrationBuilder.Sql(@"
                UPDATE seat_service.seats s
                SET seat_map_id = ss.seat_map_id
                FROM seat_service.rows r
                JOIN seat_service.sections ss ON ss.id = r.section_id
                WHERE r.id = s.row_id;
            ");

            // 3. Make NOT NULL now that all rows are populated
            migrationBuilder.AlterColumn<Guid>(
                name: "seat_map_id",
                schema: "seat_service",
                table: "seats",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // 4. Add FK + index for seat_map_id
            migrationBuilder.CreateIndex(
                name: "ix_seats_seat_map_id",
                schema: "seat_service",
                table: "seats",
                column: "seat_map_id");

            migrationBuilder.AddForeignKey(
                name: "fk_seats_seat_maps_seat_map_id",
                schema: "seat_service",
                table: "seats",
                column: "seat_map_id",
                principalSchema: "seat_service",
                principalTable: "seat_maps",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // 5. Drop old row_id FK, index, column
            migrationBuilder.DropForeignKey(
                name: "fk_seats_rows_row_id",
                schema: "seat_service",
                table: "seats");

            migrationBuilder.DropIndex(
                name: "ix_seats_row_id",
                schema: "seat_service",
                table: "seats");

            migrationBuilder.DropColumn(
                name: "row_id",
                schema: "seat_service",
                table: "seats");

            // 6. Drop rows and sections tables (no longer needed)
            migrationBuilder.DropTable(
                name: "rows",
                schema: "seat_service");

            migrationBuilder.DropTable(
                name: "sections",
                schema: "seat_service");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_seats_seat_maps_seat_map_id",
                schema: "seat_service",
                table: "seats");

            migrationBuilder.RenameColumn(
                name: "seat_map_id",
                schema: "seat_service",
                table: "seats",
                newName: "row_id");

            migrationBuilder.RenameIndex(
                name: "ix_seats_seat_map_id",
                schema: "seat_service",
                table: "seats",
                newName: "ix_seats_row_id");

            migrationBuilder.CreateTable(
                name: "sections",
                schema: "seat_service",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seat_map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    geometry = table.Column<string>(type: "jsonb", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    legend_id = table.Column<Guid>(type: "uuid", nullable: true),
                    section_type = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    style = table.Column<string>(type: "jsonb", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sections", x => x.id);
                    table.ForeignKey(
                        name: "fk_sections_seat_maps_seat_map_id",
                        column: x => x.seat_map_id,
                        principalSchema: "seat_service",
                        principalTable: "seat_maps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rows",
                schema: "seat_service",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    curve = table.Column<string>(type: "jsonb", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    seat_spacing = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rows", x => x.id);
                    table.ForeignKey(
                        name: "fk_rows_sections_section_id",
                        column: x => x.section_id,
                        principalSchema: "seat_service",
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rows_section_id",
                schema: "seat_service",
                table: "rows",
                column: "section_id");

            migrationBuilder.CreateIndex(
                name: "ix_sections_seat_map_id",
                schema: "seat_service",
                table: "sections",
                column: "seat_map_id");

            migrationBuilder.AddForeignKey(
                name: "fk_seats_rows_row_id",
                schema: "seat_service",
                table: "seats",
                column: "row_id",
                principalSchema: "seat_service",
                principalTable: "rows",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
