using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.SeatService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitNeon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "seat_service");

            migrationBuilder.CreateTable(
                name: "seat_maps",
                schema: "seat_service",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    canvas_settings = table.Column<string>(type: "jsonb", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seat_maps", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "objects",
                schema: "seat_service",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seat_map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_type = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: true),
                    geometry = table.Column<string>(type: "jsonb", nullable: true),
                    style = table.Column<string>(type: "jsonb", nullable: true),
                    z_index = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_objects", x => x.id);
                    table.ForeignKey(
                        name: "fk_objects_seat_maps_seat_map_id",
                        column: x => x.seat_map_id,
                        principalSchema: "seat_service",
                        principalTable: "seat_maps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sections",
                schema: "seat_service",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seat_map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    section_type = table.Column<string>(type: "text", nullable: false),
                    geometry = table.Column<string>(type: "jsonb", nullable: true),
                    style = table.Column<string>(type: "jsonb", nullable: true),
                    legend_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "versions",
                schema: "seat_service",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seat_map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    change_description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_versions_seat_maps_seat_map_id",
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
                    label = table.Column<string>(type: "text", nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    curve = table.Column<string>(type: "jsonb", nullable: true),
                    seat_spacing = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "seats",
                schema: "seat_service",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    seat_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    seat_type = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<string>(type: "jsonb", nullable: true),
                    legend_id = table.Column<Guid>(type: "uuid", nullable: true),
                    custom_properties = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seats", x => x.id);
                    table.ForeignKey(
                        name: "fk_seats_rows_row_id",
                        column: x => x.row_id,
                        principalSchema: "seat_service",
                        principalTable: "rows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_objects_seat_map_id",
                schema: "seat_service",
                table: "objects",
                column: "seat_map_id");

            migrationBuilder.CreateIndex(
                name: "ix_rows_section_id",
                schema: "seat_service",
                table: "rows",
                column: "section_id");

            migrationBuilder.CreateIndex(
                name: "ix_seat_maps_chart_id",
                schema: "seat_service",
                table: "seat_maps",
                column: "chart_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_seat_maps_event_id",
                schema: "seat_service",
                table: "seat_maps",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_seat_maps_organization_id",
                schema: "seat_service",
                table: "seat_maps",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_seats_row_id",
                schema: "seat_service",
                table: "seats",
                column: "row_id");

            migrationBuilder.CreateIndex(
                name: "ix_sections_seat_map_id",
                schema: "seat_service",
                table: "sections",
                column: "seat_map_id");

            migrationBuilder.CreateIndex(
                name: "ix_versions_seat_map_id",
                schema: "seat_service",
                table: "versions",
                column: "seat_map_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "objects",
                schema: "seat_service");

            migrationBuilder.DropTable(
                name: "seats",
                schema: "seat_service");

            migrationBuilder.DropTable(
                name: "versions",
                schema: "seat_service");

            migrationBuilder.DropTable(
                name: "rows",
                schema: "seat_service");

            migrationBuilder.DropTable(
                name: "sections",
                schema: "seat_service");

            migrationBuilder.DropTable(
                name: "seat_maps",
                schema: "seat_service");
        }
    }
}
