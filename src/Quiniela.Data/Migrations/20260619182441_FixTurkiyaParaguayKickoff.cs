using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiniela.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixTurkiyaParaguayKickoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Turquía vs Paraguay: corregido de 04:00 UTC (medianoche ET) a 03:00 UTC (11 PM ET / 9 PM CDM)
            migrationBuilder.Sql("""
                UPDATE m
                SET m.KickoffUtc = '2026-06-20 03:00:00'
                FROM Matches m
                INNER JOIN Teams h ON m.HomeTeamId = h.Id
                INNER JOIN Teams a ON m.AwayTeamId = a.Id
                WHERE h.Name = N'Turquía'
                  AND a.Name = N'Paraguay'
                  AND m.KickoffUtc = '2026-06-20 04:00:00'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE m
                SET m.KickoffUtc = '2026-06-20 04:00:00'
                FROM Matches m
                INNER JOIN Teams h ON m.HomeTeamId = h.Id
                INNER JOIN Teams a ON m.AwayTeamId = a.Id
                WHERE h.Name = N'Turquía'
                  AND a.Name = N'Paraguay'
                  AND m.KickoffUtc = '2026-06-20 03:00:00'
                """);
        }
    }
}
