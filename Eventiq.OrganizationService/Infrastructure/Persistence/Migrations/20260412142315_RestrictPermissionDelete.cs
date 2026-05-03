using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.OrganizationService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestrictPermissionDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Permissions_PermissionId",
                schema: "org_service",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Members_Permissions_PermissionId",
                schema: "org_service",
                table: "Members");

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Permissions_PermissionId",
                schema: "org_service",
                table: "Invitations",
                column: "PermissionId",
                principalSchema: "org_service",
                principalTable: "Permissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Permissions_PermissionId",
                schema: "org_service",
                table: "Members",
                column: "PermissionId",
                principalSchema: "org_service",
                principalTable: "Permissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Permissions_PermissionId",
                schema: "org_service",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Members_Permissions_PermissionId",
                schema: "org_service",
                table: "Members");

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Permissions_PermissionId",
                schema: "org_service",
                table: "Invitations",
                column: "PermissionId",
                principalSchema: "org_service",
                principalTable: "Permissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Permissions_PermissionId",
                schema: "org_service",
                table: "Members",
                column: "PermissionId",
                principalSchema: "org_service",
                principalTable: "Permissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
