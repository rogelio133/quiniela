using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public enum AchievementCategory { Prestige, Positive, Ironic }

public record Achievement(string Key, string Icon, string Name, string Description, AchievementCategory Category);

public static class AchievementCatalog
{
    public static readonly IReadOnlyList<Achievement> All =
    [
        new("streak", "🔥", "Racha de fuego", "5 o más aciertos de resultado seguidos", AchievementCategory.Positive),
        new("ko-sniper", "🎯", "Francotirador KO", "100% de aciertos de avance en eliminatorias, con al menos 3 pronósticos KO", AchievementCategory.Positive),
        new("comeback", "📈", "La Remontada", "Subió 3 o más posiciones entre dos partidos consecutivos", AchievementCategory.Positive),
        new("visionary", "👑", "Vidente", "Acertó qué equipo sería el campeón del mundial", AchievementCategory.Prestige),
        new("eternal-leader", "🥇", "Puntero eterno", "Estuvo en el 1er lugar en al menos el 70% de los partidos con tabla registrada", AchievementCategory.Prestige),
        new("turtle", "🐢", "Modo tortuga", "Ni un solo acierto de resultado, con al menos 10 pronósticos hechos", AchievementCategory.Ironic),
        new("bipolar", "🎪", "El Bipolar", "Alternó acierto y fallo de resultado en 5 o más partidos consecutivos", AchievementCategory.Ironic),
        new("traitor", "🪦", "El Traidor", "Apostó en contra del equipo que terminó siendo campeón del mundial", AchievementCategory.Ironic),
        new("last-minute", "⏱️", "Gol Agónico", "Envió el cambio final de su pronóstico a 30 minutos o menos del kickoff, en 2 o más partidos", AchievementCategory.Ironic),
        new("slot-machine", "🎰", "Modo Tragamonedas", "Cambió su pronóstico 3 veces o más en el mismo partido, en 2 o más partidos distintos", AchievementCategory.Ironic),
        new("sure-shot", "🗿", "Dicho y Hecho", "Nunca cambió ninguno de sus pronósticos, con al menos 10 hechos", AchievementCategory.Positive),
        new("daily-best", "🌞", "Mejor del día", "Fue quien más puntos sumó en un día de partidos. Una medalla 🏅 por cada día ganado.", AchievementCategory.Positive),
        new("daily-worst", "🥴", "Peor del día", "Fue quien menos puntos sumó en un día de partidos (no pronosticar cuenta como 0). Una medalla 🏅 por cada día... sufrido.", AchievementCategory.Ironic),
    ];

    public static Achievement Get(string key) => All.First(a => a.Key == key);
}

// Medals = 0 → sin fila de medallas (solo daily-best/daily-worst usan Medals > 0)
public record EarnedBadge(Achievement Badge, int Medals);

public class AchievementsService(
    IDbContextFactory<QuinielaDbContext> dbFactory,
    PlayerStatsService statsService,
    StandingsService standingsService,
    DailyAwardService dailyAwardService)
{
    /// <summary>
    /// Insignias obtenidas por cada miembro de la sala. Todo se calcula on-demand
    /// a partir de Predictions/StandingsSnapshot/ChampionPrediction, sin tabla nueva.
    /// </summary>
    public async Task<Dictionary<int, List<EarnedBadge>>> GetForPoolAsync(int poolId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var members = await db.PoolMembers
            .Where(m => m.PoolId == poolId)
            .Select(m => m.UserId)
            .ToListAsync();

        var history = await standingsService.GetPositionHistoryAsync(poolId);

        var championPoints = await db.ChampionPredictions
            .Where(c => c.PoolId == poolId)
            .ToDictionaryAsync(c => c.UserId, c => c.Points);

        var championTeamId = await GetRealChampionTeamIdAsync(db);

        var championMatchPredictions = championTeamId is null
            ? []
            : await db.Predictions
                .Include(p => p.Match)
                .Where(p => p.PoolId == poolId
                         && p.Match.Status == MatchStatus.Finalizado
                         && (p.Match.Stage == MatchStage.Grupos || p.Match.Stage == MatchStage.Octavos)
                         && (p.Match.HomeTeamId == championTeamId || p.Match.AwayTeamId == championTeamId))
                .ToListAsync();

        var orderedPredictions = await db.Predictions
            .Include(p => p.Match)
            .Where(p => p.PoolId == poolId && p.Match.Status == MatchStatus.Finalizado)
            .OrderBy(p => p.Match.KickoffUtc)
            .ToListAsync();
        var predictionsByUser = orderedPredictions
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var historyRows = await db.PredictionHistories
            .Include(h => h.Prediction).ThenInclude(p => p.Match)
            .Where(h => h.Prediction.PoolId == poolId)
            .ToListAsync();

        var changesByUser = historyRows
            .GroupBy(h => h.Prediction.UserId)
            .ToDictionary(g => g.Key, g => g
                .GroupBy(h => h.PredictionId)
                .Select(pg => new
                {
                    ChangeCount = pg.Count() - 1, // filas totales - la inicial
                    FinalChangeAt = pg.Max(h => h.ChangedAt),
                    pg.First().Prediction.Match.KickoffUtc
                })
                .ToList());

        var awardCounts = await dailyAwardService.GetCountsAsync(poolId);

        var result = new Dictionary<int, List<EarnedBadge>>();
        foreach (var userId in members)
        {
            var stats = await statsService.GetAsync(userId, poolId);
            var badges = new List<Achievement>();

            if (stats.BestStreak >= 5)
                badges.Add(AchievementCatalog.Get("streak"));
            if (stats.KoPredictions >= 3 && stats.KoCorrect == stats.KoPredictions)
                badges.Add(AchievementCatalog.Get("ko-sniper"));
            if (stats.TotalPredictions >= 10 && stats.CorrectResults == 0)
                badges.Add(AchievementCatalog.Get("turtle"));

            var series = history.GetValueOrDefault(userId, []);
            if (series.Count >= 2)
            {
                if (HasComeback(series))
                    badges.Add(AchievementCatalog.Get("comeback"));
                if (IsEternalLeader(series))
                    badges.Add(AchievementCatalog.Get("eternal-leader"));
            }

            if (championPoints.GetValueOrDefault(userId) > 0)
                badges.Add(AchievementCatalog.Get("visionary"));

            if (HasBipolarStreak(predictionsByUser.GetValueOrDefault(userId, [])))
                badges.Add(AchievementCatalog.Get("bipolar"));

            if (championTeamId is not null &&
                championMatchPredictions.Any(p => p.UserId == userId && BetAgainstChampion(p, championTeamId.Value)))
                badges.Add(AchievementCatalog.Get("traitor"));

            var changes = changesByUser.GetValueOrDefault(userId, []);

            if (changes.Count(c => c.KickoffUtc - c.FinalChangeAt <= TimeSpan.FromMinutes(30)) >= 2)
                badges.Add(AchievementCatalog.Get("last-minute"));

            if (changes.Count(c => c.ChangeCount > 2) >= 2)
                badges.Add(AchievementCatalog.Get("slot-machine"));

            if (changes.Count >= 10 && changes.All(c => c.ChangeCount == 0))
                badges.Add(AchievementCatalog.Get("sure-shot"));

            var earned = badges.Select(b => new EarnedBadge(b, 0)).ToList();

            var (best, worst) = awardCounts.GetValueOrDefault(userId);
            if (best >= 1)
                earned.Add(new EarnedBadge(AchievementCatalog.Get("daily-best"), best));
            if (worst >= 1)
                earned.Add(new EarnedBadge(AchievementCatalog.Get("daily-worst"), worst));

            result[userId] = earned;
        }
        return result;
    }

    private static bool HasComeback(List<StandingsService.PositionPoint> series)
    {
        for (int i = 1; i < series.Count; i++)
            if (series[i - 1].Position - series[i].Position >= 3) return true;
        return false;
    }

    private static bool IsEternalLeader(List<StandingsService.PositionPoint> series)
    {
        var firsts = series.Count(p => p.Position == 1);
        return firsts / (double)series.Count >= 0.7;
    }

    private static bool HasBipolarStreak(List<Prediction> chronological)
    {
        if (chronological.Count < 5) return false;

        int run = 1, best = 1;
        for (int i = 1; i < chronological.Count; i++)
        {
            bool prevCorrect = chronological[i - 1].PtsResult > 0;
            bool curCorrect = chronological[i].PtsResult > 0;
            run = curCorrect != prevCorrect ? run + 1 : 1;
            best = Math.Max(best, run);
        }
        return best >= 5;
    }

    private static bool BetAgainstChampion(Prediction p, int championTeamId)
    {
        bool championIsHome = p.Match.HomeTeamId == championTeamId;
        char outcomeAgainstChampion = championIsHome ? 'A' : 'H';
        return p.PredOutcome == outcomeAgainstChampion;
    }

    private static async Task<int?> GetRealChampionTeamIdAsync(QuinielaDbContext db)
    {
        var final = await db.Matches.FirstOrDefaultAsync(
            m => m.Stage == MatchStage.Final && m.Status == MatchStatus.Finalizado);

        if (final is null || final.HomeTeamId is null || final.AwayTeamId is null) return null;
        return final.HomeScore > final.AwayScore ? final.HomeTeamId : final.AwayTeamId;
    }
}
