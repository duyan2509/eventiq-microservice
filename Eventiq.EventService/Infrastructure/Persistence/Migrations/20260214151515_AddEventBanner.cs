using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.EventService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventBanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventBanner",
                table: "Events",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventBanner",
                table: "Events");
        }
    }
}
