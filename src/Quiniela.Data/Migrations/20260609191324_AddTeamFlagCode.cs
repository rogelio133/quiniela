using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiniela.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamFlagCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FlagCode",
                table: "Teams",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlagCode",
                table: "Teams");
        }
    }
}
