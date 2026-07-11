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
        new("daily-best", "🌞", "Mejor del día", "Fue quien más puntos sumó en un día de partidos, sin empatar con nadie. Una medalla 🏅 por cada día ganado.", AchievementCategory.Positive),
        new("daily-worst", "🥴", "Peor del día", "Fue quien menos puntos sumó en un día de partidos, sin empatar con nadie (no pronosticar cuenta como 0). Una medalla 🏅 por cada día... sufrido.", AchievementCategory.Ironic),
        new("group-master", "🧠", "Dueño del Grupo", "Acertó el resultado de los 6 partidos de un mismo grupo", AchievementCategory.Positive),
        new("penalty-prophet", "🔮", "Profeta de Penales", "Pronosticó que un partido de eliminatorias se definiría en penales… y así fue. Una medalla 🏅 por cada profecía cumplida.", AchievementCategory.Prestige),
        new("heartbroken", "💔", "Corazón Roto", "El equipo que eligió como campeón ya fue eliminado del mundial", AchievementCategory.Ironic),
        new("lone-wolf", "🐺", "Lobo Solitario", "Fue el único de la sala en acertar un partido. Una medalla 🏅 por cada hazaña.", AchievementCategory.Positive),
        new("black-sheep", "🐑", "Oveja Negra", "Fue el único de la sala en fallar un partido que todos los demás acertaron. Una medalla 🏅 por cada resbalón.", AchievementCategory.Ironic),
        new("optimist", "🙅", "El Optimista", "Nunca pronosticó un empate en fase de grupos, con al menos 10 pronósticos de grupos hechos", AchievementCategory.Ironic),
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

        var championPicks = await db.ChampionPredictions
            .Where(c => c.PoolId == poolId)
            .ToDictionaryAsync(c => c.UserId, c => new { c.Points, c.TeamId });

        var eliminatedTeamIds = championPicks.Count == 0
            ? new HashSet<int>()
            : await GetEliminatedTeamIdsAsync(db);

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

        // 🐺/🐑 por partido finalizado con ≥3 pronósticos en la sala: lobo = el único que acertó
        // (sin pronóstico cuenta como fallo); oveja = el único que falló entre los que pronosticaron.
        // Con ≥3 pronósticos ambas condiciones son incompatibles en el mismo partido.
        var wolfCounts = new Dictionary<int, int>();
        var sheepCounts = new Dictionary<int, int>();
        foreach (var matchGroup in orderedPredictions.GroupBy(p => p.MatchId))
        {
            var preds = matchGroup.ToList();
            if (preds.Count < 3) continue;

            var correct = preds.Where(p => p.PtsResult > 0).ToList();
            if (correct.Count == 1)
            {
                var wolf = correct[0].UserId;
                wolfCounts[wolf] = wolfCounts.GetValueOrDefault(wolf) + 1;
            }
            else if (correct.Count == preds.Count - 1)
            {
                var sheep = preds.First(p => p.PtsResult == 0).UserId;
                sheepCounts[sheep] = sheepCounts.GetValueOrDefault(sheep) + 1;
            }
        }

        var result = new Dictionary<int, List<EarnedBadge>>();
        foreach (var userId in members)
        {
            var stats = await statsService.GetAsync(userId, poolId);
            var userPreds = predictionsByUser.GetValueOrDefault(userId, []);
            var badges = new List<Achievement>();

            if (stats.BestStreak >= 5)
                badges.Add(AchievementCatalog.Get("streak"));
            if (stats.KoPredictions >= 3 && stats.KoCorrect == stats.KoPredictions)
                badges.Add(AchievementCatalog.Get("ko-sniper"));
            if (stats.TotalPredictions >= 10 && stats.CorrectResults == 0)
                badges.Add(AchievementCatalog.Get("turtle"));

            var groupPreds = userPreds.Where(p => p.Match.Stage == MatchStage.Grupos).ToList();

            // El conteo es contra 6 fijo (formato 2026: 6 partidos por grupo), no contra
            // "los que pronosticó": pronosticar 4 y acertar 4 no cuenta.
            if (groupPreds.Where(p => p.Match.GroupCode != null)
                    .GroupBy(p => p.Match.GroupCode)
                    .Any(g => g.Count() == 6 && g.All(p => p.PtsResult > 0)))
                badges.Add(AchievementCatalog.Get("group-master"));

            // Solo grupos: en KO el empate no es un desenlace pronosticable
            if (groupPreds.Count >= 10 && groupPreds.All(p => p.PredOutcome != 'D'))
                badges.Add(AchievementCatalog.Get("optimist"));

            var series = history.GetValueOrDefault(userId, []);
            if (series.Count >= 2)
            {
                if (HasComeback(series))
                    badges.Add(AchievementCatalog.Get("comeback"));
                if (IsEternalLeader(series))
                    badges.Add(AchievementCatalog.Get("eternal-leader"));
            }

            var championPick = championPicks.GetValueOrDefault(userId);

            if (championPick?.Points > 0)
                badges.Add(AchievementCatalog.Get("visionary"));

            if (championPick is not null && eliminatedTeamIds.Contains(championPick.TeamId))
                badges.Add(AchievementCatalog.Get("heartbroken"));

            if (HasBipolarStreak(userPreds))
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

            // Acierto de instancia = PredInstance == DecidedIn, sin mirar PtsInstance:
            // queda insensible a la configuración de PtsBonusKO de la sala.
            var prophecies = userPreds.Count(p => p.Match.Stage != MatchStage.Grupos
                && p.PredInstance == MatchDecidedIn.Penalties
                && p.Match.DecidedIn == MatchDecidedIn.Penalties);
            if (prophecies >= 1)
                earned.Add(new EarnedBadge(AchievementCatalog.Get("penalty-prophet"), prophecies));

            var wolves = wolfCounts.GetValueOrDefault(userId);
            if (wolves >= 1)
                earned.Add(new EarnedBadge(AchievementCatalog.Get("lone-wolf"), wolves));

            var sheep = sheepCounts.GetValueOrDefault(userId);
            if (sheep >= 1)
                earned.Add(new EarnedBadge(AchievementCatalog.Get("black-sheep"), sheep));

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

    /// <summary>
    /// Equipos eliminados del mundial (para 💔 Corazón Roto), calculado una vez por sala:
    /// (1) perdió un partido KO finalizado — excepto TercerLugar, que no elimina a nadie del
    /// título: sus dos equipos ya cayeron en semifinales; o (2) toda la fase de grupos terminó,
    /// los 16avos están completamente asignados, y el equipo no aparece en ningún cruce. La
    /// condición doble de (2) evita falsos positivos a media asignación de cruces.
    /// </summary>
    private static async Task<HashSet<int>> GetEliminatedTeamIdsAsync(QuinielaDbContext db)
    {
        var eliminated = (await db.Matches
            .Where(m => m.Status == MatchStatus.Finalizado
                     && m.Stage != MatchStage.Grupos && m.Stage != MatchStage.TercerLugar
                     && m.HomeTeamId != null && m.AwayTeamId != null)
            .Select(m => m.HomeScore > m.AwayScore ? m.AwayTeamId!.Value : m.HomeTeamId!.Value)
            .ToListAsync())
            .ToHashSet();

        var groupsDone = !await db.Matches.AnyAsync(
            m => m.Stage == MatchStage.Grupos && m.Status != MatchStatus.Finalizado);
        if (groupsDone)
        {
            var dieciseisavos = await db.Matches
                .Where(m => m.Stage == MatchStage.Dieciseisavos)
                .Select(m => new { m.HomeTeamId, m.AwayTeamId })
                .ToListAsync();

            if (dieciseisavos.Count > 0 && dieciseisavos.All(m => m.HomeTeamId != null && m.AwayTeamId != null))
            {
                var qualified = dieciseisavos
                    .SelectMany(m => new[] { m.HomeTeamId!.Value, m.AwayTeamId!.Value })
                    .ToHashSet();
                var allTeamIds = await db.Teams.Select(t => t.Id).ToListAsync();
                foreach (var teamId in allTeamIds.Where(id => !qualified.Contains(id)))
                    eliminated.Add(teamId);
            }
        }
        return eliminated;
    }

    private static async Task<int?> GetRealChampionTeamIdAsync(QuinielaDbContext db)
    {
        var final = await db.Matches.FirstOrDefaultAsync(
            m => m.Stage == MatchStage.Final && m.Status == MatchStatus.Finalizado);

        if (final is null || final.HomeTeamId is null || final.AwayTeamId is null) return null;
        return final.HomeScore > final.AwayScore ? final.HomeTeamId : final.AwayTeamId;
    }
}
