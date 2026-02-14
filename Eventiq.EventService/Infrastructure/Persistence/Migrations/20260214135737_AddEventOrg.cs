using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.EventService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventOrg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OranizationAvatar",
                table: "Events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "OrganizationName",
                table: "Events",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OranizationAvatar",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "OrganizationName",
                table: "Events");
        }
    }
}
