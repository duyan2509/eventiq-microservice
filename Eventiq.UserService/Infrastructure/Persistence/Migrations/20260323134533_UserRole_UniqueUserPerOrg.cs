using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.UserService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserRole_UniqueUserPerOrg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRoles",
                schema: "user_service",
                table: "UserRoles");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRoles",
                schema: "user_service",
                table: "UserRoles",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_OrganizationId",
                schema: "user_service",
                table: "UserRoles",
                columns: new[] { "UserId", "OrganizationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRoles",
                schema: "user_service",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_UserId_OrganizationId",
                schema: "user_service",
                table: "UserRoles");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRoles",
                schema: "user_service",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId" });
        }
    }
}
