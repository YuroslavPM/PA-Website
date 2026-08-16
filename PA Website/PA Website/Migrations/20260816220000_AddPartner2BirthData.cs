using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PA_Website.Migrations
{
    /// <inheritdoc />
    public partial class AddPartner2BirthData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Partner2AstrologicalDate",
                table: "userServices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Partner2PlaceOfBirth",
                table: "userServices",
                type: "longtext",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE userServices SET ReservationDate = UTC_TIMESTAMP() WHERE ReservationDate < '2000-01-01'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Partner2AstrologicalDate",
                table: "userServices");

            migrationBuilder.DropColumn(
                name: "Partner2PlaceOfBirth",
                table: "userServices");
        }
    }
}
