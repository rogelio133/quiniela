using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class ScoringService(IDbContextFactory<QuinielaDbContext> dbFactory, StandingsService standingsService, PushNotificationService pushService)
{
    public async Task RecalculateForMatchAsync(int matchId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var match = await db.Matches.FindAsync(matchId);
        if (match is null || match.HomeScore is null || match.AwayScore is null)
            return;

        char realOutcome = match.HomeScore > match.AwayScore ? 'H'
                         : match.HomeScore < match.AwayScore ? 'A'
                         : 'D';

        bool isKnockout = match.Stage != MatchStage.Grupos;

        var predictions = await db.Predictions
            .Where(p => p.MatchId == matchId)
            .Include(p => p.Pool)
            .ToListAsync();

        foreach (var pred in predictions)
        {
            pred.PtsResult = pred.PredOutcome == realOutcome ? pred.Pool.PtsCorrect : 0;

            pred.PtsInstance = isKnockout && match.DecidedIn is not null && pred.PredInstance == match.DecidedIn
                ? pred.Pool.PtsBonusKO
                : 0;

            pred.Points = pred.PtsResult + pred.PtsInstance;
        }

        await db.SaveChangesAsync();

        await NotifyResultAsync(db, match, predictions);

        if (match.Stage == MatchStage.Final)
            await ResolveChampionAsync(db, match);

        await SaveSnapshotAsync(db, matchId);
    }

    /// <summary>
    /// N1: notifica a cada jugador si acertó o falló el partido recién finalizado. Un usuario en
    /// varias salas recibe una sola notificación (el partido es el mismo), usando la primera
    /// predicción encontrada para el link y el desglose de puntos.
    /// </summary>
    private async Task NotifyResultAsync(QuinielaDbContext db, Match match, List<Prediction> predictions)
    {
        if (predictions.Count == 0) return;

        var homeTeam = await db.Teams.FindAsync(match.HomeTeamId);
        var awayTeam = await db.Teams.FindAsync(match.AwayTeamId);
        var matchLabel = $"{homeTeam?.ShortCode ?? "?"} {match.HomeScore}-{match.AwayScore} {awayTeam?.ShortCode ?? "?"}";

        foreach (var group in predictions.GroupBy(p => p.UserId))
        {
            var pred = group.OrderBy(p => p.PoolId).First();
            var body = pred.PtsResult > 0
                ? $"✅ Acertaste — +{pred.Points} pts"
                : "❌ Fallaste esta vez";

            await pushService.SendAsync(pred.UserId, $"⚽ {matchLabel}", body, $"/pools/{pred.PoolId}/predictions");
        }
    }

    private static async Task ResolveChampionAsync(QuinielaDbContext db, Match final)
    {
        if (final.HomeTeamId is null || final.AwayTeamId is null) return;

        int championTeamId = final.HomeScore > final.AwayScore ? final.HomeTeamId.Value : final.AwayTeamId.Value;

        var championPredictions = await db.ChampionPredictions.ToListAsync();
        if (championPredictions.Count == 0) return;

        var poolPts = await db.Pools.ToDictionaryAsync(p => p.Id, p => p.PtsChampion);

        foreach (var cp in championPredictions)
            cp.Points = cp.TeamId == championTeamId ? poolPts.GetValueOrDefault(cp.PoolId) : 0;

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Saves the standings snapshot for <paramref name="matchId"/>, bounded to the standings as they
    /// were at that match's kickoff (not "as of today"). When correcting a result, this also cascades:
    /// any already-saved later snapshot in the same pool (KickoffUtc &gt;= this match's) is recomputed
    /// too, since a correction to an earlier match changes every standings cut taken after it.
    /// </summary>
    private async Task SaveSnapshotAsync(QuinielaDbContext db, int matchId)
    {
        var kickoff = await db.Matches
            .Where(m => m.Id == matchId)
            .Select(m => m.KickoffUtc)
            .SingleAsync();

        var affectedPoolIds = await db.Predictions
            .Where(p => p.MatchId == matchId)
            .Select(p => p.PoolId)
            .Distinct()
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var poolId in affectedPoolIds)
        {
            // N3: posiciones previas a este partido, para comparar tras el recálculo (por usuario).
            var previousPositions = (await db.StandingsSnapshots
                .Where(s => s.PoolId == poolId && s.Match.KickoffUtc < kickoff)
                .OrderByDescending(s => s.Match.KickoffUtc)
                .Select(s => new { s.UserId, s.Position })
                .ToListAsync())
                .GroupBy(s => s.UserId)
                .ToDictionary(g => g.Key, g => g.First().Position);

            var matchIdsToRefresh = await db.StandingsSnapshots
                .Where(s => s.PoolId == poolId && s.Match.KickoffUtc >= kickoff)
                .Select(s => s.MatchId)
                .Distinct()
                .ToListAsync();

            if (!matchIdsToRefresh.Contains(matchId))
                matchIdsToRefresh.Add(matchId);

            var kickoffsByMatch = await db.Matches
                .Where(m => matchIdsToRefresh.Contains(m.Id))
                .Select(m => new { m.Id, m.KickoffUtc })
                .ToDictionaryAsync(x => x.Id, x => x.KickoffUtc);

            await db.StandingsSnapshots
                .Where(s => s.PoolId == poolId && matchIdsToRefresh.Contains(s.MatchId))
                .ExecuteDeleteAsync();

            List<StandingsSnapshot>? triggeringSnapshots = null;

            foreach (var mId in matchIdsToRefresh)
            {
                var standings = await standingsService.GetStandingsAsync(db, poolId, kickoffsByMatch[mId]);
                var positions = StandingsService.ComputePositions(standings);
                var snapshots = standings.Select((row, idx) => new StandingsSnapshot
                {
                    PoolId = poolId,
                    MatchId = mId,
                    UserId = row.UserId,
                    Position = positions[idx],
                    Points = row.TotalPoints,
                    SavedAt = now,
                }).ToList();
                db.StandingsSnapshots.AddRange(snapshots);

                if (mId == matchId)
                    triggeringSnapshots = snapshots;
            }

            await db.SaveChangesAsync();

            // N3: solo se notifica el cambio de posición del partido que disparó este recálculo,
            // no de los partidos futuros recomputados en cascada por una corrección.
            if (triggeringSnapshots is not null)
                await NotifyPositionChangesAsync(db, poolId, previousPositions, triggeringSnapshots);
        }
    }

    private async Task NotifyPositionChangesAsync(
        QuinielaDbContext db, int poolId, Dictionary<int, int> previousPositions, List<StandingsSnapshot> snapshots)
    {
        var pool = await db.Pools.FindAsync(poolId);
        if (pool is null) return;

        foreach (var snap in snapshots)
        {
            if (!previousPositions.TryGetValue(snap.UserId, out var prevPos) || prevPos == snap.Position)
                continue;

            var direction = snap.Position < prevPos ? "⬆️ Subiste" : "⬇️ Bajaste";
            await pushService.SendAsync(snap.UserId,
                $"{direction} al {snap.Position}° lugar",
                $"Sala: {pool.Name}",
                $"/pools/{poolId}/standings");
        }
    }
}
