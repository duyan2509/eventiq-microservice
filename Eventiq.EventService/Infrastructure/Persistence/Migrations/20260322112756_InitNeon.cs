using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.EventService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitNeon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "event_service");

            migrationBuilder.CreateTable(
                name: "events",
                schema: "event_service",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_name = table.Column<string>(type: "text", nullable: false),
                    oranization_avatar = table.Column<string>(type: "text", nullable: true),
                    event_banner = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    detail_address = table.Column<string>(type: "text", nullable: true),
                    province_code = table.Column<string>(type: "text", nullable: true),
                    commune_code = table.Column<string>(type: "text", nullable: true),
                    province_name = table.Column<string>(type: "text", nullable: true),
                    commune_name = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "charts",
                schema: "event_service",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_charts", x => x.id);
                    table.ForeignKey(
                        name: "fk_charts_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "event_service",
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "legends",
                schema: "event_service",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    color = table.Column<string>(type: "text", nullable: true),
                    price = table.Column<int>(type: "integer", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legends", x => x.id);
                    table.ForeignKey(
                        name: "fk_legends_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "event_service",
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submissions",
                schema: "event_service",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    admin_email = table.Column<string>(type: "text", nullable: false),
                    admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_submissions_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "event_service",
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                schema: "event_service",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_sessions_charts_chart_id",
                        column: x => x.chart_id,
                        principalSchema: "event_service",
                        principalTable: "charts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sessions_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "event_service",
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_charts_event_id",
                schema: "event_service",
                table: "charts",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_legends_event_id",
                schema: "event_service",
                table: "legends",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_chart_id",
                schema: "event_service",
                table: "sessions",
                column: "chart_id");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_event_id",
                schema: "event_service",
                table: "sessions",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_submissions_event_id",
                schema: "event_service",
                table: "submissions",
                column: "event_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legends",
                schema: "event_service");

            migrationBuilder.DropTable(
                name: "sessions",
                schema: "event_service");

            migrationBuilder.DropTable(
                name: "submissions",
                schema: "event_service");

            migrationBuilder.DropTable(
                name: "charts",
                schema: "event_service");

            migrationBuilder.DropTable(
                name: "events",
                schema: "event_service");
        }
    }
}
