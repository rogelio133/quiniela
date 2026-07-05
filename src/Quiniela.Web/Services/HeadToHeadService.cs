using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class HeadToHeadService(IDbContextFactory<QuinielaDbContext> dbFactory)
{
    public record H2HRow(
        int MatchId,
        MatchStage Stage,
        string MatchLabel,
        DateTime KickoffUtc,
        string? HomeFlagCode, string? HomeCode,
        string? AwayFlagCode, string? AwayCode,
        char? PredA, int PtsA,
        char? PredB, int PtsB);

    /// <summary>
    /// Compares two pool members match by match (finalized matches only), ordered by KickoffUtc.
    /// Matches where only one of the two predicted are included (the other's PredX = null, PtsX = 0)
    /// so the accumulated total isn't distorted; the UI marks those rows as one-sided instead of
    /// counting them toward either player's "matches ganados" tally.
    /// </summary>
    public async Task<List<H2HRow>> CompareAsync(int poolId, int userAId, int userBId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var matches = await db.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.Status == MatchStatus.Finalizado
                     && m.Predictions.Any(p => p.PoolId == poolId && (p.UserId == userAId || p.UserId == userBId)))
            .OrderBy(m => m.KickoffUtc)
            .ToListAsync();

        var matchIds = matches.Select(m => m.Id).ToList();
        var preds = await db.Predictions
            .Where(p => p.PoolId == poolId
                     && matchIds.Contains(p.MatchId)
                     && (p.UserId == userAId || p.UserId == userBId))
            .ToListAsync();

        return matches.Select(m =>
        {
            var predA = preds.FirstOrDefault(p => p.MatchId == m.Id && p.UserId == userAId);
            var predB = preds.FirstOrDefault(p => p.MatchId == m.Id && p.UserId == userBId);
            return new H2HRow(
                m.Id,
                m.Stage,
                $"{m.HomeTeam?.ShortCode ?? m.HomeSlotLabel ?? "?"} vs {m.AwayTeam?.ShortCode ?? m.AwaySlotLabel ?? "?"}",
                m.KickoffUtc,
                m.HomeTeam?.FlagCode, m.HomeTeam?.ShortCode ?? m.HomeTeam?.Name,
                m.AwayTeam?.FlagCode, m.AwayTeam?.ShortCode ?? m.AwayTeam?.Name,
                predA?.PredOutcome, predA?.Points ?? 0,
                predB?.PredOutcome, predB?.Points ?? 0);
        }).ToList();
    }
}
