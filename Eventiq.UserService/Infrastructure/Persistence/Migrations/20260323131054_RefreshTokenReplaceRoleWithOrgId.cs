using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.UserService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefreshTokenReplaceRoleWithOrgId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentRole",
                schema: "user_service",
                table: "RefreshTokens");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                schema: "user_service",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrganizationId",
                schema: "user_service",
                table: "RefreshTokens");

            migrationBuilder.AddColumn<int>(
                name: "CurrentRole",
                schema: "user_service",
                table: "RefreshTokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
