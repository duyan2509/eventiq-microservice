using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.UserService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgIdToUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "UserRoles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "BanHistories",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "UserRoles");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "BanHistories",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
