using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderSettledBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "settled_by",
                schema: "payment_service",
                table: "orders",
                type: "text",
                nullable: true);

            // Backfill: orders already Paid were settled by the webhook (reconciliation did not
            // exist before this migration), so attribute them to Webhook rather than leaving NULL.
            migrationBuilder.Sql(
                "UPDATE payment_service.orders SET settled_by = 'Webhook' WHERE status = 'Paid';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "settled_by",
                schema: "payment_service",
                table: "orders");
        }
    }
}
