using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.SeatService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FlatSeatTypeIntAndLabelUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Change seat_type from text to integer
            // Regular→1, Wheelchair→2, Companion→3, Restricted→4, anything else→1
            migrationBuilder.Sql(@"
                ALTER TABLE seat_service.seats
                    ALTER COLUMN seat_type TYPE integer
                    USING CASE seat_type
                        WHEN 'Regular'     THEN 1
                        WHEN 'Wheelchair'  THEN 2
                        WHEN 'Companion'   THEN 3
                        WHEN 'Restricted'  THEN 4
                        ELSE 1
                    END;
            ");

            // Unique label per seat map, soft-delete aware
            migrationBuilder.CreateIndex(
                name: "ix_seats_seat_map_id_label",
                schema: "seat_service",
                table: "seats",
                columns: new[] { "seat_map_id", "label" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_seats_seat_map_id_label",
                schema: "seat_service",
                table: "seats");

            migrationBuilder.Sql(@"
                ALTER TABLE seat_service.seats
                    ALTER COLUMN seat_type TYPE text
                    USING CASE seat_type
                        WHEN 1 THEN 'Regular'
                        WHEN 2 THEN 'Wheelchair'
                        WHEN 3 THEN 'Companion'
                        WHEN 4 THEN 'Restricted'
                        ELSE 'Regular'
                    END;
            ");
        }
    }
}
