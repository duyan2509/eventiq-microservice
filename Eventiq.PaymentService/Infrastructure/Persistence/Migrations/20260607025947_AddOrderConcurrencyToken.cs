using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.PaymentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "payment_service",
                table: "orders",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "payment_service",
                table: "orders");
        }
    }
}
