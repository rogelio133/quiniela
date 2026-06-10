using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiniela.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVenueAndShortCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortCode",
                table: "Teams",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Venue",
                table: "Matches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortCode",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "Venue",
                table: "Matches");
        }
    }
}
