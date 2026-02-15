using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventiq.EventService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixEventSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Submissions");
        }
    }
}
