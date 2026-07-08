using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class StandingsService(IDbContextFactory<QuinielaDbContext> dbFactory)
{
    public record StandingEntry(
        int UserId,
        string DisplayName,
        string? ProfilePicturePath,
        int TotalPoints,
        int CorrectPredictions,
        int TotalPredictions);

    /// <summary>
    /// Returns all pool members sorted by TotalPoints DESC, CorrectPredictions DESC.
    /// Members with zero predictions are included with zeroed stats.
    /// </summary>
    public async Task<List<StandingEntry>> GetStandingsAsync(int poolId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await GetStandingsAsync(db, poolId, asOfKickoffUtc: null);
    }

    /// <summary>
    /// Same as <see cref="GetStandingsAsync(int)"/> but usable with a caller-owned db context, and
    /// optionally bounded to only count predictions for matches kicked off at or before <paramref name="asOfKickoffUtc"/>.
    /// Used by <see cref="ScoringService"/> to compute "standings as of match X" for historical snapshots,
    /// as opposed to "standings as of today".
    /// </summary>
    public async Task<List<StandingEntry>> GetStandingsAsync(QuinielaDbContext db, int poolId, DateTime? asOfKickoffUtc)
    {
        var members = await db.PoolMembers
            .Where(m => m.PoolId == poolId)
            .Select(m => new { m.UserId, m.User.DisplayName, m.User.ProfilePicturePath })
            .ToListAsync();

        var predictionsQuery = db.Predictions.Where(p => p.PoolId == poolId);
        if (asOfKickoffUtc is not null)
            predictionsQuery = predictionsQuery.Where(p => p.Match.KickoffUtc <= asOfKickoffUtc.Value);

        // Aggregate in SQL: SUM(Points), COUNT(Points > 0), COUNT(*)
        var aggregates = await predictionsQuery
            .GroupBy(p => p.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPoints = g.Sum(p => p.Points),
                CorrectPredictions = g.Count(p => p.PtsResult > 0),
                TotalPredictions = g.Count()
            })
            .ToDictionaryAsync(x => x.UserId);

        // Champion points only ever exist once the Final match is finalized; when bounding by
        // date, only count them if the Final's kickoff already happened by that point in time.
        bool includeChampionPoints = asOfKickoffUtc is null || await db.Matches
            .Where(m => m.Stage == MatchStage.Final && m.KickoffUtc <= asOfKickoffUtc.Value)
            .AnyAsync();

        var championPoints = includeChampionPoints
            ? await db.ChampionPredictions
                .Where(c => c.PoolId == poolId)
                .ToDictionaryAsync(c => c.UserId, c => c.Points)
            : [];

        return [.. members
            .Select(m =>
            {
                aggregates.TryGetValue(m.UserId, out var agg);
                championPoints.TryGetValue(m.UserId, out var champPts);
                return new StandingEntry(
                    m.UserId,
                    m.DisplayName,
                    m.ProfilePicturePath,
                    (agg?.TotalPoints ?? 0) + champPts,
                    agg?.CorrectPredictions ?? 0,
                    agg?.TotalPredictions ?? 0);
            })
            .OrderByDescending(e => e.TotalPoints)
            .ThenByDescending(e => e.CorrectPredictions)
            .ThenBy(e => e.DisplayName)];
    }

    public async Task<int> GetFinalizedMatchCountAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Matches.CountAsync(m => m.Status == MatchStatus.Finalizado);
    }

    /// <summary>
    /// Tie-aware ranking: entries with the same points and correct-prediction count share a position.
    /// </summary>
    public static List<int> ComputePositions(IReadOnlyList<StandingEntry> list)
    {
        var pos = new int[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            if (i == 0) { pos[i] = 1; continue; }
            bool tied = list[i].TotalPoints == list[i - 1].TotalPoints
                     && list[i].CorrectPredictions == list[i - 1].CorrectPredictions;
            pos[i] = tied ? pos[i - 1] : i + 1;
        }
        return [.. pos];
    }

    public record PositionPoint(int MatchId, DateTime KickoffUtc, int Position, string MatchLabel);

    /// <summary>
    /// Full position history per user, one point per finalized match with a saved snapshot,
    /// ordered chronologically by KickoffUtc (not by SavedAt, which reflects capture order).
    /// </summary>
    public async Task<Dictionary<int, List<PositionPoint>>> GetPositionHistoryAsync(int poolId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var snapshots = await db.StandingsSnapshots
            .Include(s => s.Match).ThenInclude(m => m.HomeTeam)
            .Include(s => s.Match).ThenInclude(m => m.AwayTeam)
            .Where(s => s.PoolId == poolId)
            .OrderBy(s => s.Match.KickoffUtc)
            .ToListAsync();

        return snapshots
            .GroupBy(s => s.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(s => new PositionPoint(
                    s.MatchId,
                    s.Match.KickoffUtc,
                    s.Position,
                    $"{s.Match.HomeTeam?.ShortCode ?? s.Match.HomeSlotLabel ?? "?"} vs {s.Match.AwayTeam?.ShortCode ?? s.Match.AwaySlotLabel ?? "?"}"))
                    .ToList());
    }

    /// <summary>
    /// Positions from the most recent standings snapshot for the pool, keyed by UserId.
    /// Returns an empty dictionary if no snapshot exists yet (e.g. first finalized match).
    /// </summary>
    public async Task<Dictionary<int, int>> GetLastSnapshotPositionsAsync(int poolId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // Ordered by the match's kickoff, not by SavedAt: cascaded recomputes (Fix 3) and the
        // group-stage backfill (Fix 5) rewrite/create many snapshots sharing one SavedAt, so
        // SavedAt no longer identifies the chronologically latest match — KickoffUtc does.
        var lastMatchId = await db.StandingsSnapshots
            .Where(s => s.PoolId == poolId)
            .OrderByDescending(s => s.Match.KickoffUtc)
            .ThenByDescending(s => s.MatchId)
            .Select(s => (int?)s.MatchId)
            .FirstOrDefaultAsync();

        if (lastMatchId is null) return [];

        return await db.StandingsSnapshots
            .Where(s => s.PoolId == poolId && s.MatchId == lastMatchId)
            .ToDictionaryAsync(s => s.UserId, s => s.Position);
    }
}
