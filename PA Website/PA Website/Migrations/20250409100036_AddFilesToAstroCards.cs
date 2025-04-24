using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PA_Website.Migrations
{
    /// <inheritdoc />
    public partial class AddFilesToAstroCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AstroCardContentType",
                table: "userServices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AstroCardFileName",
                table: "userServices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AstroCardFilePath",
                table: "userServices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AstroCardFileSize",
                table: "userServices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AstroCardUploadDate",
                table: "userServices",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AstroCardContentType",
                table: "userServices");

            migrationBuilder.DropColumn(
                name: "AstroCardFileName",
                table: "userServices");

            migrationBuilder.DropColumn(
                name: "AstroCardFilePath",
                table: "userServices");

            migrationBuilder.DropColumn(
                name: "AstroCardFileSize",
                table: "userServices");

            migrationBuilder.DropColumn(
                name: "AstroCardUploadDate",
                table: "userServices");
        }
    }
}
