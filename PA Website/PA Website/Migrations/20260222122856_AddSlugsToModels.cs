using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PA_Website.Migrations
{
    /// <inheritdoc />
    public partial class AddSlugsToModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Service",
                type: "longtext",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Promotions",
                type: "longtext",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Articles",
                type: "longtext",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Articles");
        }
    }
}
