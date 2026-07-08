using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiniela.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeNotificationLogMatchIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_UserId_MatchId_Type",
                table: "NotificationLogs");

            migrationBuilder.AlterColumn<int>(
                name: "MatchId",
                table: "NotificationLogs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_UserId_MatchId_Type",
                table: "NotificationLogs",
                columns: new[] { "UserId", "MatchId", "Type" },
                unique: true,
                filter: "[MatchId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_UserId_MatchId_Type",
                table: "NotificationLogs");

            migrationBuilder.AlterColumn<int>(
                name: "MatchId",
                table: "NotificationLogs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_UserId_MatchId_Type",
                table: "NotificationLogs",
                columns: new[] { "UserId", "MatchId", "Type" },
                unique: true);
        }
    }
}
