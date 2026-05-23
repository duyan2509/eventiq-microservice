using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.UserService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgNameToRefreshToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrgName",
                schema: "user_service",
                table: "RefreshTokens",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrgName",
                schema: "user_service",
                table: "RefreshTokens");
        }
    }
}
