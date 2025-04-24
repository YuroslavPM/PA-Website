using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PA_Website.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusToUserService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "userServices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "userServices");
        }
    }
}
