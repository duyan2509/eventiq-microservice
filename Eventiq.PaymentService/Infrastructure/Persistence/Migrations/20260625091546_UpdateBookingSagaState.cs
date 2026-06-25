using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBookingSagaState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "seat_items_json",
                schema: "payment_service",
                table: "booking_sagas",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "session_id",
                schema: "payment_service",
                table: "booking_sagas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "seat_items_json",
                schema: "payment_service",
                table: "booking_sagas");

            migrationBuilder.DropColumn(
                name: "session_id",
                schema: "payment_service",
                table: "booking_sagas");
        }
    }
}
