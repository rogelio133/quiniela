using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class StandingsService(QuinielaDbContext db)
{
    public record StandingEntry(
        int UserId,
        string DisplayName,
        int TotalPoints,
        int CorrectPredictions,
        int TotalPredictions);

    /// <summary>
    /// Returns all pool members sorted by TotalPoints DESC, CorrectPredictions DESC.
    /// Members with zero predictions are included with zeroed stats.
    /// </summary>
    public async Task<List<StandingEntry>> GetStandingsAsync(int poolId)
    {
        var members = await db.PoolMembers
            .Where(m => m.PoolId == poolId)
            .Select(m => new { m.UserId, m.User.DisplayName })
            .ToListAsync();

        // Aggregate in SQL: SUM(Points), COUNT(Points > 0), COUNT(*)
        var aggregates = await db.Predictions
            .Where(p => p.PoolId == poolId)
            .GroupBy(p => p.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPoints = g.Sum(p => p.Points),
                CorrectPredictions = g.Count(p => p.Points > 0),
                TotalPredictions = g.Count()
            })
            .ToDictionaryAsync(x => x.UserId);

        return [.. members
            .Select(m =>
            {
                aggregates.TryGetValue(m.UserId, out var agg);
                return new StandingEntry(
                    m.UserId,
                    m.DisplayName,
                    agg?.TotalPoints ?? 0,
                    agg?.CorrectPredictions ?? 0,
                    agg?.TotalPredictions ?? 0);
            })
            .OrderByDescending(e => e.TotalPoints)
            .ThenByDescending(e => e.CorrectPredictions)
            .ThenBy(e => e.DisplayName)];
    }

    public async Task<int> GetFinalizedMatchCountAsync() =>
        await db.Matches.CountAsync(m => m.Status == MatchStatus.Finalizado);
}
