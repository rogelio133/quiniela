using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

/// <summary>
/// Datos del "Resumen final del torneo" (/pools/{poolId}/final-summary).
/// Orquesta servicios existentes (StandingsService, AchievementsService, DailyAwardService,
/// PlayerStatsService) en vez de duplicar queries; las stats curiosas de sala (RF3/RF4)
/// sí son queries propias de este servicio.
/// </summary>
public class FinalSummaryService(
    IDbContextFactory<QuinielaDbContext> dbFactory,
    StandingsService standingsService,
    AchievementsService achievementsService,
    DailyAwardService dailyAwardService)
{
    // Misma zona fija que DailyAwardService/DailySummaryService: sin DST desde 2022.
    private static readonly TimeZoneInfo Tz =
        TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

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

    // ── RF3: stats curiosas de la sala ──────────────────────────────────────
    // Un record por stat del catálogo del doc 13; null = no computable aún con
    // los datos actuales (la tarjeta simplemente no se renderiza).

    public record MemberRef(int UserId, string DisplayName, string? ProfilePicturePath);
    public record MatchRef(int MatchId, string Label, DateTime KickoffUtc);

    public record CollectiveFailStat(MatchRef Match, int FailedCount, int MoreMatches);   // #1 💀
    public record StreakStat(List<MemberRef> Holders, int Length);                        // #2 🔥 / #8 🥶
    public record LoneWolfStat(MemberRef Wolf, MatchRef Match, int OthersFailed);         // #3 🐺
    public record ComebackStat(MemberRef Member, int FromPos, int ToPos,
                               string FromLabel, string ToLabel);                         // #4 🚀
    public record AgonicChangeStat(MemberRef Member, MatchRef Match,
                                   int MinutesBefore, bool? Hit);                         // #6 ⏰
    public record IndecisiveStat(MemberRef Member, int TotalChanges,
                                 MatchRef? HotMatch, int HotMatchChanges);                // #7 🎰
    public record TeamMoodStat(string TeamName, string FlagCode, int Value);              // #9 🪦 / #10 🧲
    public record GoldenDayStat(DateOnly Day, int Points);                                // #11 (dorado)
    public record BlackDayStat(DateOnly Day, int CorrectCount, int TotalCount);           // #11 (negro)
    public record SurpriseStat(MatchRef Match, string UnanimousPickLabel);                // #12 🤯
    public record DecidedItAllStat(string FromLabel, bool FromStart,
                                   int MatchesLeading, int FinalMargin);                  // #13 🏆
    public record DrawAllergyStat(int RealDraws, int RealMatches,
                                  int PredictedDraws, int PredictedTotal);                // #15 ⚖️
    public record ObviousStat(int Count, MatchRef? Example);                              // #18 🤝
    public record ProphetRow(MemberRef Member, int Count);
    public record PenaltyProphetsStat(int PenaltyMatches, List<ProphetRow> Prophets);     // #19 🥅
    public record GhostStat(MemberRef? Ghost, int GhostMissed, int PoolMissedTotal);      // #20 💸
    public record DailyMedalsStat(MemberRef? Best, int BestCount,
                                  MemberRef? Worst, int WorstCount);                      // #21 🌞🥴
    public record ChampionFaithRow(MemberRef Member, string TeamName, string FlagCode,
                                   bool Hit, bool Eliminated, DateOnly? HeartbrokenOn);   // #22 👑
    public record StageKingsStat(MemberRef? GroupKing, int GroupPts,
                                 MemberRef? KoKing, int KoPts);                           // #23 🕐
    public record TypicalHourRow(MemberRef Member, int Hour);
    public record NightOwlStat(MemberRef Member, DateTime LocalTime, string MatchLabel,
                               List<TypicalHourRow> TypicalHours);                        // #24 🌙
    public record AnticipationStat(MemberRef EarlyBird, TimeSpan EarlyAvg,
                                   MemberRef LastMinute, TimeSpan LateAvg);               // #25 ⚡
    public record UnmovedStat(int UnchangedCount, int TotalPredictions);                  // #26 🗿
    public record LuckyVenueStat(string Venue, int CorrectCount, int TotalCount,
                                 int MatchCount);                                         // #28 🗺️
    public record ConstantStat(MemberRef Member, int ModePosition, double PctAtMode);     // #29 📊
    public record TotalsStat(int Predictions, int Changes, int FinalizedMatches,
                             int RealGoals, int BadgesAwarded, int TournamentDays);       // #30 🔢

    public record ShowcaseEntry(MemberRef Member, List<EarnedBadge> Badges);

    public class PoolStats
    {
        public CollectiveFailStat? CollectiveFail { get; init; }
        public StreakStat? BestStreak { get; init; }
        public LoneWolfStat? LoneWolf { get; init; }
        public ComebackStat? Comeback { get; init; }
        public AgonicChangeStat? AgonicChange { get; init; }
        public IndecisiveStat? Indecisive { get; init; }
        public StreakStat? WorstStreak { get; init; }
        public TeamMoodStat? CursedTeam { get; init; }
        public TeamMoodStat? CharmTeam { get; init; }
        public GoldenDayStat? GoldenDay { get; init; }
        public BlackDayStat? BlackDay { get; init; }
        public SurpriseStat? Surprise { get; init; }
        public DecidedItAllStat? DecidedItAll { get; init; }
        public int? LeaderChanges { get; init; }
        public DrawAllergyStat? DrawAllergy { get; init; }
        public int NobodySawItCount { get; init; }
        public ObviousStat? Obvious { get; init; }
        public PenaltyProphetsStat? PenaltyProphets { get; init; }
        public GhostStat? Ghost { get; init; }
        public DailyMedalsStat? DailyMedals { get; init; }
        public List<ChampionFaithRow> ChampionFaith { get; init; } = [];
        public StageKingsStat? StageKings { get; init; }
        public NightOwlStat? NightOwl { get; init; }
        public AnticipationStat? Anticipation { get; init; }
        public UnmovedStat? Unmoved { get; init; }
        public LuckyVenueStat? LuckyVenue { get; init; }
        public ConstantStat? MostConstant { get; init; }
        public required TotalsStat Totals { get; init; }
        public required List<ShowcaseEntry> Showcase { get; init; }
    }

    /// <summary>
    /// Todas las stats de sala seleccionadas en el catálogo del doc 13 + la vitrina
    /// de insignias, calculadas on-demand (se auto-corrigen si el admin corrige un
    /// marcador). Con la Final sin capturar devuelve datos parciales — suficiente
    /// para la vista previa del admin.
    /// </summary>
    public async Task<PoolStats> GetPoolStatsAsync(int poolId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var members = await db.PoolMembers
            .Where(m => m.PoolId == poolId)
            .Select(m => new MemberRef(m.UserId, m.User.DisplayName, m.User.ProfilePicturePath))
            .ToListAsync();
        var memberById = members.ToDictionary(m => m.UserId);

        var finalized = await db.Matches
            .Include(m => m.HomeTeam).Include(m => m.AwayTeam)
            .Where(m => m.Status == MatchStatus.Finalizado)
            .OrderBy(m => m.KickoffUtc)
            .ToListAsync();
        var matchById = finalized.ToDictionary(m => m.Id);

        var preds = await db.Predictions
            .Where(p => p.PoolId == poolId && p.Match.Status == MatchStatus.Finalizado)
            .Select(p => new { p.UserId, p.MatchId, p.PredOutcome, p.PredInstance, p.Points, p.PtsResult })
            .ToListAsync();
        var predsByMatch = preds.GroupBy(p => p.MatchId).ToDictionary(g => g.Key, g => g.ToList());
        var predsByUser = preds.GroupBy(p => p.UserId).ToDictionary(
            g => g.Key,
            g => g.OrderBy(p => matchById[p.MatchId].KickoffUtc).ToList());

        int totalPoolPredictions = await db.Predictions.CountAsync(p => p.PoolId == poolId);

        // Historial de capturas/cambios (K.1): la primera fila por Prediction es la
        // captura inicial; cada fila posterior es un cambio real.
        var historyRows = await db.PredictionHistories
            .Where(h => h.Prediction.PoolId == poolId)
            .Select(h => new
            {
                h.PredictionId,
                h.Prediction.UserId,
                h.Prediction.MatchId,
                h.Prediction.Match.KickoffUtc,
                h.ChangedAt,
                h.Prediction.PtsResult,
                MatchFinalized = h.Prediction.Match.Status == MatchStatus.Finalizado,
                HomeLabel = h.Prediction.Match.HomeTeam != null
                    ? (h.Prediction.Match.HomeTeam.ShortCode ?? h.Prediction.Match.HomeTeam.Name)
                    : (h.Prediction.Match.HomeSlotLabel ?? "?"),
                AwayLabel = h.Prediction.Match.AwayTeam != null
                    ? (h.Prediction.Match.AwayTeam.ShortCode ?? h.Prediction.Match.AwayTeam.Name)
                    : (h.Prediction.Match.AwaySlotLabel ?? "?")
            })
            .ToListAsync();

        var championPicks = await db.ChampionPredictions
            .Include(c => c.Team)
            .Where(c => c.PoolId == poolId)
            .ToListAsync();

        var eliminatedTeamIds = championPicks.Count == 0
            ? new HashSet<int>()
            : await AchievementsService.GetEliminatedTeamIdsAsync(db);

        var finalMatch = finalized.FirstOrDefault(m => m.Stage == MatchStage.Final);
        int? realChampionTeamId = finalMatch is { HomeTeamId: not null, AwayTeamId: not null }
            ? (finalMatch.HomeScore > finalMatch.AwayScore ? finalMatch.HomeTeamId : finalMatch.AwayTeamId)
            : null;

        var history = await standingsService.GetPositionHistoryAsync(poolId);
        var standings = await standingsService.GetStandingsAsync(poolId);
        var awardCounts = await dailyAwardService.GetCountsAsync(poolId);
        var badgesByUser = await achievementsService.GetForPoolAsync(poolId);

        int memberCount = members.Count;

        static string SideLabel(Match m, bool home) => home
            ? m.HomeTeam?.ShortCode ?? m.HomeTeam?.Name ?? m.HomeSlotLabel ?? "?"
            : m.AwayTeam?.ShortCode ?? m.AwayTeam?.Name ?? m.AwaySlotLabel ?? "?";
        static MatchRef Ref(Match m) => new(
            m.Id, $"{SideLabel(m, true)} {m.HomeScore}-{m.AwayScore} {SideLabel(m, false)}", m.KickoffUtc);
        static DateOnly LocalDay(DateTime utc) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, Tz));

        // #1 💀 El engaño colectivo — todos pronosticaron y todos fallaron
        CollectiveFailStat? collectiveFail = null;
        if (memberCount >= 2)
        {
            var allFailed = finalized
                .Where(m => predsByMatch.TryGetValue(m.Id, out var ps)
                         && ps.Count == memberCount && ps.All(p => p.PtsResult == 0))
                .ToList();
            if (allFailed.Count > 0)
                collectiveFail = new(Ref(allFailed[^1]), memberCount, allFailed.Count - 1);
        }

        // #2 🔥 / #8 🥶 — mejor racha de aciertos y peor racha de fallos de la sala
        StreakStat? bestStreak = null, worstStreak = null;
        {
            var bestByUser = new Dictionary<int, int>();
            var worstByUser = new Dictionary<int, int>();
            foreach (var (userId, list) in predsByUser)
            {
                int hitRun = 0, missRun = 0, bestHit = 0, bestMiss = 0;
                foreach (var p in list)
                {
                    if (p.PtsResult > 0) { hitRun++; missRun = 0; }
                    else { missRun++; hitRun = 0; }
                    bestHit = Math.Max(bestHit, hitRun);
                    bestMiss = Math.Max(bestMiss, missRun);
                }
                bestByUser[userId] = bestHit;
                worstByUser[userId] = bestMiss;
            }
            if (bestByUser.Count > 0 && bestByUser.Values.Max() >= 2)
            {
                int len = bestByUser.Values.Max();
                bestStreak = new(bestByUser.Where(kv => kv.Value == len)
                    .Select(kv => memberById[kv.Key]).OrderBy(m => m.DisplayName).ToList(), len);
            }
            if (worstByUser.Count > 0 && worstByUser.Values.Max() >= 2)
            {
                int len = worstByUser.Values.Max();
                worstStreak = new(worstByUser.Where(kv => kv.Value == len)
                    .Select(kv => memberById[kv.Key]).OrderBy(m => m.DisplayName).ToList(), len);
            }
        }

        // #3 🐺 El acierto más solitario — único en acertar, los demás fallaron (≥3 preds)
        LoneWolfStat? loneWolf = null;
        foreach (var m in finalized)
        {
            if (!predsByMatch.TryGetValue(m.Id, out var ps) || ps.Count < 3) continue;
            var correct = ps.Where(p => p.PtsResult > 0).ToList();
            if (correct.Count != 1) continue;
            int othersFailed = ps.Count - 1;
            if (loneWolf is null || othersFailed > loneWolf.OthersFailed
                || (othersFailed == loneWolf.OthersFailed && m.KickoffUtc > loneWolf.Match.KickoffUtc))
                loneWolf = new(memberById[correct[0].UserId], Ref(m), othersFailed);
        }

        // #4 🚀 La remontada — mayor subida de posiciones del torneo
        ComebackStat? comeback = null;
        foreach (var (userId, series) in history)
        {
            if (!memberById.ContainsKey(userId)) continue;
            int worstPos = 0, worstIdx = -1;
            for (int i = 0; i < series.Count; i++)
            {
                if (series[i].Position > worstPos) { worstPos = series[i].Position; worstIdx = i; }
                int climb = worstPos - series[i].Position;
                if (climb > 0 && (comeback is null || climb > comeback.FromPos - comeback.ToPos))
                    comeback = new(memberById[userId], worstPos, series[i].Position,
                                   series[worstIdx].MatchLabel, series[i].MatchLabel);
            }
        }

        // Cambios reales por predicción (filas − 1) — base de #6/#7/#25/#26
        var changesByPrediction = historyRows
            .GroupBy(h => h.PredictionId)
            .Select(g =>
            {
                var ordered = g.OrderBy(h => h.ChangedAt).ToList();
                return new
                {
                    ordered[0].UserId,
                    ordered[0].MatchId,
                    ordered[0].KickoffUtc,
                    ordered[0].PtsResult,
                    ordered[0].MatchFinalized,
                    HomeLabel = ordered[0].HomeLabel,
                    AwayLabel = ordered[0].AwayLabel,
                    Changes = ordered.Count - 1,
                    ChangeTimes = ordered.Skip(1).Select(h => h.ChangedAt).ToList(),
                    FinalCaptureAt = ordered[^1].ChangedAt
                };
            })
            .ToList();

        // #6 ⏰ El cambio más agónico — cambio real más cercano al kickoff
        AgonicChangeStat? agonic = null;
        foreach (var c in changesByPrediction.Where(c => c.Changes > 0))
        {
            foreach (var t in c.ChangeTimes)
            {
                var minutes = (int)Math.Floor((c.KickoffUtc - t).TotalMinutes);
                if (minutes < 0) continue;
                if (agonic is null || minutes < agonic.MinutesBefore)
                {
                    var label = matchById.TryGetValue(c.MatchId, out var mm)
                        ? Ref(mm).Label
                        : $"{c.HomeLabel} vs {c.AwayLabel}";
                    agonic = new(memberById[c.UserId], new MatchRef(c.MatchId, label, c.KickoffUtc),
                                 minutes, c.MatchFinalized ? c.PtsResult > 0 : null);
                }
            }
        }

        // #7 🎰 El más indeciso — más cambios totales + el partido más cambiado de la sala
        IndecisiveStat? indecisive = null;
        {
            var byUser = changesByPrediction.GroupBy(c => c.UserId)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.Changes));
            if (byUser.Count > 0 && byUser.Values.Max() > 0)
            {
                var top = byUser.OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => memberById[kv.Key].DisplayName).First();

                var byMatch = changesByPrediction.GroupBy(c => c.MatchId)
                    .Select(g => new { g.First().HomeLabel, g.First().AwayLabel, MatchId = g.Key, g.First().KickoffUtc, Changes = g.Sum(c => c.Changes) })
                    .Where(x => x.Changes > 0)
                    .OrderByDescending(x => x.Changes)
                    .FirstOrDefault();

                MatchRef? hot = null;
                if (byMatch is not null)
                    hot = matchById.TryGetValue(byMatch.MatchId, out var hm)
                        ? Ref(hm)
                        : new MatchRef(byMatch.MatchId, $"{byMatch.HomeLabel} vs {byMatch.AwayLabel}", byMatch.KickoffUtc);

                indecisive = new(memberById[top.Key], top.Value, hot, byMatch?.Changes ?? 0);
            }
        }

        // #9 🪦 / #10 🧲 — equipo maldito (más fallos provocó) y talismán (más puntos regaló)
        TeamMoodStat? cursed = null, charm = null;
        {
            var fails = new Dictionary<int, int>();
            var gifts = new Dictionary<int, int>();
            var teamById = new Dictionary<int, Team>();
            foreach (var p in preds)
            {
                var m = matchById[p.MatchId];
                foreach (var team in new[] { m.HomeTeam, m.AwayTeam })
                {
                    if (team is null) continue;
                    teamById[team.Id] = team;
                    if (p.PtsResult == 0) fails[team.Id] = fails.GetValueOrDefault(team.Id) + 1;
                    gifts[team.Id] = gifts.GetValueOrDefault(team.Id) + p.Points;
                }
            }
            if (fails.Count > 0 && fails.Values.Max() > 0)
            {
                var t = fails.OrderByDescending(kv => kv.Value).ThenBy(kv => teamById[kv.Key].Name).First();
                cursed = new(teamById[t.Key].Name, teamById[t.Key].FlagCode, t.Value);
            }
            if (gifts.Count > 0 && gifts.Values.Max() > 0)
            {
                var t = gifts.OrderByDescending(kv => kv.Value).ThenBy(kv => teamById[kv.Key].Name).First();
                charm = new(teamById[t.Key].Name, teamById[t.Key].FlagCode, t.Value);
            }
        }

        // #11 📅 El día dorado / el día negro (día local CDMX, patrón DailyAwardService)
        GoldenDayStat? goldenDay = null;
        BlackDayStat? blackDay = null;
        {
            var pointsByDay = new Dictionary<DateOnly, int>();
            var hitsByDay = new Dictionary<DateOnly, (int Correct, int Total)>();
            foreach (var p in preds)
            {
                var day = LocalDay(matchById[p.MatchId].KickoffUtc);
                pointsByDay[day] = pointsByDay.GetValueOrDefault(day) + p.Points;
                var (c, t) = hitsByDay.GetValueOrDefault(day);
                hitsByDay[day] = (c + (p.PtsResult > 0 ? 1 : 0), t + 1);
            }
            // El día de la Final se suman los puntos de campeón (criterio Módulo L)
            if (finalMatch is not null)
            {
                var finalDay = LocalDay(finalMatch.KickoffUtc);
                pointsByDay[finalDay] = pointsByDay.GetValueOrDefault(finalDay)
                                        + championPicks.Sum(c => c.Points);
            }
            if (pointsByDay.Count > 0)
            {
                var gold = pointsByDay.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First();
                goldenDay = new(gold.Key, gold.Value);
            }
            var black = hitsByDay.Where(kv => kv.Value.Total >= 3)
                .OrderBy(kv => kv.Value.Correct / (double)kv.Value.Total)
                .ThenBy(kv => kv.Key)
                .Select(kv => new BlackDayStat(kv.Key, kv.Value.Correct, kv.Value.Total))
                .FirstOrDefault();
            blackDay = black;
        }

        // #12 🤯 La sorpresa del torneo — pronóstico unánime… y salió lo contrario
        SurpriseStat? surprise = null;
        if (memberCount >= 2)
        {
            foreach (var m in finalized)
            {
                if (!predsByMatch.TryGetValue(m.Id, out var ps) || ps.Count != memberCount) continue;
                char pick = ps[0].PredOutcome;
                if (ps.Any(p => p.PredOutcome != pick)) continue;
                char actual = m.HomeScore > m.AwayScore ? 'H' : m.HomeScore < m.AwayScore ? 'A' : 'D';
                if (actual == pick) continue;
                string pickLabel = pick switch
                {
                    'H' => $"todos con {m.HomeTeam?.Name ?? SideLabel(m, true)}",
                    'A' => $"todos con {m.AwayTeam?.Name ?? SideLabel(m, false)}",
                    _ => "todos con el empate"
                };
                if (surprise is null || m.KickoffUtc > surprise.Match.KickoffUtc)
                    surprise = new(Ref(m), pickLabel);
            }
        }

        // #13 🏆 El partido que decidió todo — desde cuándo el campeón no soltó el 1°
        DecidedItAllStat? decidedItAll = null;
        if (standings.Count >= 2 && history.TryGetValue(standings[0].UserId, out var champSeries)
            && champSeries.Count > 0 && champSeries[^1].Position == 1)
        {
            int lastNonOne = champSeries.FindLastIndex(p => p.Position != 1);
            int from = lastNonOne + 1;
            decidedItAll = new(
                champSeries[from].MatchLabel,
                FromStart: from == 0,
                MatchesLeading: champSeries.Count - from,
                FinalMargin: standings[0].TotalPoints - standings[1].TotalPoints);
        }

        // #14 🔀 La guerra por la cima — cuántas veces cambió el líder
        int? leaderChanges = null;
        {
            var leadersByMatch = history
                .SelectMany(kv => kv.Value.Select(p => new { p.MatchId, p.KickoffUtc, kv.Key, p.Position }))
                .GroupBy(x => x.MatchId)
                .Select(g => new
                {
                    g.First().KickoffUtc,
                    Leaders = g.Where(x => x.Position == 1).Select(x => x.Key).OrderBy(x => x).ToList()
                })
                .OrderBy(x => x.KickoffUtc)
                .ToList();
            if (leadersByMatch.Count >= 2)
            {
                int changes = 0;
                for (int i = 1; i < leadersByMatch.Count; i++)
                    if (!leadersByMatch[i].Leaders.SequenceEqual(leadersByMatch[i - 1].Leaders))
                        changes++;
                leaderChanges = changes;
            }
        }

        // #15 ⚖️ Alergia al empate — % de empates reales vs % pronosticados (grupos)
        DrawAllergyStat? drawAllergy = null;
        {
            var groupMatches = finalized.Where(m => m.Stage == MatchStage.Grupos).ToList();
            var groupPreds = preds.Where(p => matchById[p.MatchId].Stage == MatchStage.Grupos).ToList();
            if (groupMatches.Count > 0 && groupPreds.Count > 0)
                drawAllergy = new(
                    groupMatches.Count(m => m.HomeScore == m.AwayScore), groupMatches.Count,
                    groupPreds.Count(p => p.PredOutcome == 'D'), groupPreds.Count);
        }

        // #17 🙈 Nadie lo vio venir — partidos donde nadie de la sala acertó
        int nobodySawIt = finalized.Count(m =>
            predsByMatch.TryGetValue(m.Id, out var ps) && ps.Count > 0 && ps.All(p => p.PtsResult == 0));

        // #18 🤝 El partido obvio — el 100% de la sala acertó
        ObviousStat? obvious = null;
        if (memberCount >= 2)
        {
            var all = finalized
                .Where(m => predsByMatch.TryGetValue(m.Id, out var ps)
                         && ps.Count == memberCount && ps.All(p => p.PtsResult > 0))
                .ToList();
            if (all.Count > 0)
                obvious = new(all.Count, Ref(all[^1]));
        }

        // #19 🥅 Los que olieron los penales
        PenaltyProphetsStat? penaltyProphets = null;
        {
            var penaltyMatchIds = finalized
                .Where(m => m.Stage != MatchStage.Grupos && m.DecidedIn == MatchDecidedIn.Penalties)
                .Select(m => m.Id)
                .ToHashSet();
            if (finalized.Any(m => m.Stage != MatchStage.Grupos))
            {
                var prophets = preds
                    .Where(p => penaltyMatchIds.Contains(p.MatchId)
                             && p.PredInstance == MatchDecidedIn.Penalties)
                    .GroupBy(p => p.UserId)
                    .Select(g => new ProphetRow(memberById[g.Key], g.Count()))
                    .OrderByDescending(r => r.Count).ThenBy(r => r.Member.DisplayName)
                    .ToList();
                penaltyProphets = new(penaltyMatchIds.Count, prophets);
            }
        }

        // #20 💸 Puntos dejados en la mesa — partidos finalizados sin pronosticar
        GhostStat? ghost = null;
        if (finalized.Count > 0 && memberCount > 0)
        {
            var missedByUser = members.ToDictionary(
                m => m.UserId,
                m => finalized.Count - (predsByUser.TryGetValue(m.UserId, out var list) ? list.Count : 0));
            int poolMissed = missedByUser.Values.Sum();
            var worst = missedByUser.OrderByDescending(kv => kv.Value)
                .ThenBy(kv => memberById[kv.Key].DisplayName).First();
            ghost = new(worst.Value > 0 ? memberById[worst.Key] : null, worst.Value, poolMissed);
        }

        // #21 🌞🥴 Medallero del día a día
        DailyMedalsStat? dailyMedals = null;
        if (awardCounts.Count > 0)
        {
            var best = awardCounts.Where(kv => kv.Value.Best > 0)
                .OrderByDescending(kv => kv.Value.Best).ThenBy(kv => memberById[kv.Key].DisplayName)
                .Select(kv => (Member: memberById[kv.Key], kv.Value.Best)).FirstOrDefault();
            var worst = awardCounts.Where(kv => kv.Value.Worst > 0)
                .OrderByDescending(kv => kv.Value.Worst).ThenBy(kv => memberById[kv.Key].DisplayName)
                .Select(kv => (Member: memberById[kv.Key], kv.Value.Worst)).FirstOrDefault();
            if (best.Member is not null || worst.Member is not null)
                dailyMedals = new(best.Member, best.Item2, worst.Member, worst.Item2);
        }

        // #22 👑 Fe en el campeón — picks de todos, quién acertó y los 💔
        var championFaith = new List<ChampionFaithRow>();
        foreach (var pick in championPicks)
        {
            if (!memberById.TryGetValue(pick.UserId, out var member)) continue;
            bool hit = pick.Points > 0 || (realChampionTeamId is not null && pick.TeamId == realChampionTeamId);
            bool eliminated = !hit && eliminatedTeamIds.Contains(pick.TeamId);
            DateOnly? heartbrokenOn = null;
            if (eliminated)
            {
                // Partido KO donde su equipo cayó; si murió en grupos, su último partido de grupos
                var koLoss = finalized.FirstOrDefault(m =>
                    m.Stage != MatchStage.Grupos && m.Stage != MatchStage.TercerLugar
                    && ((m.HomeTeamId == pick.TeamId && m.HomeScore < m.AwayScore)
                     || (m.AwayTeamId == pick.TeamId && m.AwayScore < m.HomeScore)));
                var death = koLoss ?? finalized.LastOrDefault(m =>
                    m.Stage == MatchStage.Grupos
                    && (m.HomeTeamId == pick.TeamId || m.AwayTeamId == pick.TeamId));
                if (death is not null) heartbrokenOn = LocalDay(death.KickoffUtc);
            }
            championFaith.Add(new(member, pick.Team.Name, pick.Team.FlagCode, hit, eliminated, heartbrokenOn));
        }
        championFaith = [.. championFaith
            .OrderByDescending(r => r.Hit)
            .ThenBy(r => r.Eliminated)
            .ThenBy(r => r.Member.DisplayName)];

        // #23 🕐 Rey de grupos vs rey del KO
        StageKingsStat? stageKings = null;
        {
            var groupPts = new Dictionary<int, int>();
            var koPts = new Dictionary<int, int>();
            foreach (var p in preds)
            {
                var dict = matchById[p.MatchId].Stage == MatchStage.Grupos ? groupPts : koPts;
                dict[p.UserId] = dict.GetValueOrDefault(p.UserId) + p.Points;
            }
            var gk = groupPts.Where(kv => kv.Value > 0)
                .OrderByDescending(kv => kv.Value).ThenBy(kv => memberById[kv.Key].DisplayName)
                .Select(kv => (Member: memberById[kv.Key], Pts: kv.Value)).FirstOrDefault();
            var kk = koPts.Where(kv => kv.Value > 0)
                .OrderByDescending(kv => kv.Value).ThenBy(kv => memberById[kv.Key].DisplayName)
                .Select(kv => (Member: memberById[kv.Key], Pts: kv.Value)).FirstOrDefault();
            if (gk.Member is not null || kk.Member is not null)
                stageKings = new(gk.Member, gk.Pts, kk.Member, kk.Pts);
        }

        // #24 🌙 El búho — la captura a la hora más rara (más cercana a las 3:30 AM
        // CDMX, distancia circular) + hora habitual de pronosticar por jugador
        NightOwlStat? nightOwl = null;
        if (historyRows.Count > 0)
        {
            static int NightScore(DateTime local)
            {
                int mins = local.Hour * 60 + local.Minute;
                int d = Math.Abs(mins - 210); // 3:30 AM
                return Math.Min(d, 1440 - d);
            }
            var owl = historyRows
                .Where(h => memberById.ContainsKey(h.UserId))
                .Select(h => new { h, Local = TimeZoneInfo.ConvertTimeFromUtc(h.ChangedAt, Tz) })
                .OrderBy(x => NightScore(x.Local)).ThenBy(x => x.h.ChangedAt)
                .FirstOrDefault();
            var typical = historyRows
                .GroupBy(h => h.UserId)
                .Where(g => memberById.ContainsKey(g.Key))
                .Select(g => new TypicalHourRow(
                    memberById[g.Key],
                    g.GroupBy(h => TimeZoneInfo.ConvertTimeFromUtc(h.ChangedAt, Tz).Hour)
                     .OrderByDescending(hg => hg.Count()).ThenBy(hg => hg.Key)
                     .First().Key))
                .OrderBy(r => r.Hour).ThenBy(r => r.Member.DisplayName)
                .ToList();
            if (owl is not null)
                nightOwl = new(memberById[owl.h.UserId], owl.Local,
                               $"{owl.h.HomeLabel} vs {owl.h.AwayLabel}", typical);
        }

        // #25 ⚡ El madrugador vs el del último minuto — anticipación promedio al kickoff
        AnticipationStat? anticipation = null;
        {
            var avgByUser = changesByPrediction
                .Where(c => c.KickoffUtc >= c.FinalCaptureAt)
                .GroupBy(c => c.UserId)
                .Where(g => memberById.ContainsKey(g.Key))
                .Select(g => new
                {
                    Member = memberById[g.Key],
                    Avg = TimeSpan.FromTicks((long)g.Average(c => (c.KickoffUtc - c.FinalCaptureAt).Ticks))
                })
                .OrderByDescending(x => x.Avg)
                .ToList();
            if (avgByUser.Count >= 2)
                anticipation = new(avgByUser[0].Member, avgByUser[0].Avg,
                                   avgByUser[^1].Member, avgByUser[^1].Avg);
        }

        // #26 🗿 Los inamovibles — % de pronósticos que jamás se cambiaron
        UnmovedStat? unmoved = null;
        if (totalPoolPredictions > 0)
        {
            int changed = changesByPrediction.Count(c => c.Changes > 0);
            unmoved = new(totalPoolPredictions - changed, totalPoolPredictions);
        }

        // #28 🗺️ El estadio de la suerte — sede con mejor % de aciertos (≥2 partidos)
        LuckyVenueStat? luckyVenue = null;
        {
            var byVenue = finalized
                .Where(m => m.Venue != null && predsByMatch.ContainsKey(m.Id))
                .GroupBy(m => m.Venue!)
                .Select(g => new
                {
                    Venue = g.Key,
                    MatchCount = g.Count(),
                    Correct = g.Sum(m => predsByMatch[m.Id].Count(p => p.PtsResult > 0)),
                    Total = g.Sum(m => predsByMatch[m.Id].Count)
                })
                .Where(v => v.MatchCount >= 2 && v.Total > 0)
                .OrderByDescending(v => v.Correct / (double)v.Total)
                .ThenByDescending(v => v.MatchCount)
                .FirstOrDefault();
            if (byVenue is not null)
                luckyVenue = new(byVenue.Venue, byVenue.Correct, byVenue.Total, byVenue.MatchCount);
        }

        // #29 📊 El más constante — quien menos se movió de posición en el torneo
        ConstantStat? mostConstant = null;
        {
            double bestStd = double.MaxValue;
            foreach (var (userId, series) in history)
            {
                if (!memberById.ContainsKey(userId) || series.Count < 2) continue;
                var positions = series.Select(p => (double)p.Position).ToList();
                double mean = positions.Average();
                double std = Math.Sqrt(positions.Average(p => (p - mean) * (p - mean)));
                if (std < bestStd)
                {
                    bestStd = std;
                    var mode = series.GroupBy(p => p.Position)
                        .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First();
                    mostConstant = new(memberById[userId], mode.Key,
                                       mode.Count() / (double)series.Count);
                }
            }
        }

        // #30 🔢 La sala en números totales (cierre tipo Wrapped)
        var totals = new TotalsStat(
            Predictions: totalPoolPredictions,
            Changes: changesByPrediction.Sum(c => c.Changes),
            FinalizedMatches: finalized.Count,
            RealGoals: finalized.Sum(m => (m.HomeScore ?? 0) + (m.AwayScore ?? 0)),
            BadgesAwarded: badgesByUser.Values.Sum(list => list.Count),
            TournamentDays: finalized.Select(m => LocalDay(m.KickoffUtc)).Distinct().Count());

        // Vitrina definitiva — mismo orden que Achievements (más insignias primero)
        var showcase = standings
            .Select(e => new ShowcaseEntry(
                new MemberRef(e.UserId, e.DisplayName, e.ProfilePicturePath),
                badgesByUser.GetValueOrDefault(e.UserId, [])))
            .OrderByDescending(s => s.Badges.Count)
            .ThenBy(s => s.Member.DisplayName)
            .ToList();

        return new PoolStats
        {
            CollectiveFail = collectiveFail,
            BestStreak = bestStreak,
            LoneWolf = loneWolf,
            Comeback = comeback,
            AgonicChange = agonic,
            Indecisive = indecisive,
            WorstStreak = worstStreak,
            CursedTeam = cursed,
            CharmTeam = charm,
            GoldenDay = goldenDay,
            BlackDay = blackDay,
            Surprise = surprise,
            DecidedItAll = decidedItAll,
            LeaderChanges = leaderChanges,
            DrawAllergy = drawAllergy,
            NobodySawItCount = nobodySawIt,
            Obvious = obvious,
            PenaltyProphets = penaltyProphets,
            Ghost = ghost,
            DailyMedals = dailyMedals,
            ChampionFaith = championFaith,
            StageKings = stageKings,
            NightOwl = nightOwl,
            Anticipation = anticipation,
            Unmoved = unmoved,
            LuckyVenue = luckyVenue,
            MostConstant = mostConstant,
            Totals = totals,
            Showcase = showcase
        };
    }
}
