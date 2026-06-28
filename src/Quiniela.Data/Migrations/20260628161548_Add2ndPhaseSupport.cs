using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiniela.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add2ndPhaseSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PredInstance",
                table: "Predictions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AwaySlotLabel",
                table: "Matches",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BracketOrder",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DecidedIn",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeSlotLabel",
                table: "Matches",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PredInstance",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "AwaySlotLabel",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "BracketOrder",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "DecidedIn",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "HomeSlotLabel",
                table: "Matches");
        }
    }
}
