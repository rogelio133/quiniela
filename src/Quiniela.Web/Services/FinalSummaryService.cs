using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

/// <summary>
/// Datos del "Resumen final del torneo" (/pools/{poolId}/final-summary).
/// Orquesta servicios existentes (StandingsService, AchievementsService, DailyAwardService,
/// PlayerStatsService) en vez de duplicar queries; las stats curiosas de sala (RF3/RF4)
/// sí serán queries propias de este servicio.
/// </summary>
public class FinalSummaryService(
    IDbContextFactory<QuinielaDbContext> dbFactory,
    StandingsService standingsService)
{
    /// <summary>
    /// Gate de visibilidad de la página: se desbloquea sola para todos cuando el
    /// resultado de la Final queda capturado — cero cambios de código al publicar.
    /// Un solo query, cacheable en el ciclo de vida de la página.
    /// </summary>
    public async Task<bool> IsUnlockedAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Matches.AnyAsync(m => m.Stage == MatchStage.Final
                                           && m.Status == MatchStatus.Finalizado);
    }

    public record PodiumEntry(int Position, StandingsService.StandingEntry Entry);

    public record Podium(
        List<PodiumEntry> Top,          // hasta 3 entradas, orden 1° → 3°
        int DeltaVsSecond,              // puntos del campeón sobre el 2° (0 si no hay 2°)
        int TotalMembers);

    /// <summary>
    /// Top 3 de la sala con posiciones tie-aware (misma lógica que Standings/Home).
    /// Con la Final sin capturar devuelve el "campeón parcial" — usado por la vista
    /// previa del admin, se auto-corrige on-demand al capturar/corregir resultados.
    /// </summary>
    public async Task<Podium> GetPodiumAsync(int poolId)
    {
        var standings = await standingsService.GetStandingsAsync(poolId);
        var positions = StandingsService.ComputePositions(standings);

        var top = standings
            .Take(3)
            .Select((e, i) => new PodiumEntry(positions[i], e))
            .ToList();

        int delta = standings.Count >= 2
            ? standings[0].TotalPoints - standings[1].TotalPoints
            : 0;

        return new Podium(top, delta, standings.Count);
    }
}
