using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PA_Website.Migrations
{
    /// <inheritdoc />
    public partial class EmailSendAgreement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailSend",
                table: "AspNetUsers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailSend",
                table: "AspNetUsers");
        }
    }
}
