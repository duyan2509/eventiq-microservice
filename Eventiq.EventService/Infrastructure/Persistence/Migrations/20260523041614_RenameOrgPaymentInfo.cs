using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.EventService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameOrgPaymentInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "org_payment_infos",
                schema: "event_service",
                newName: "org_payment_info",
                newSchema: "event_service");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "org_payment_info",
                schema: "event_service",
                newName: "org_payment_infos",
                newSchema: "event_service");
        }
    }
}
