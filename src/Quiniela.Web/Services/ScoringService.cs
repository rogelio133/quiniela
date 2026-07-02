using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class ScoringService(IDbContextFactory<QuinielaDbContext> dbFactory, StandingsService standingsService)
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
        await SaveSnapshotAsync(db, matchId);
    }

    private async Task SaveSnapshotAsync(QuinielaDbContext db, int matchId)
    {
        var poolIds = await db.Predictions
            .Where(p => p.MatchId == matchId)
            .Select(p => p.PoolId)
            .Distinct()
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var poolId in poolIds)
        {
            var standings = await standingsService.GetStandingsAsync(poolId);
            var positions = StandingsService.ComputePositions(standings);
            var snapshots = standings.Select((row, idx) => new StandingsSnapshot
            {
                PoolId = poolId,
                MatchId = matchId,
                UserId = row.UserId,
                Position = positions[idx],
                Points = row.TotalPoints,
                SavedAt = now,
            });
            db.StandingsSnapshots.AddRange(snapshots);
        }

        await db.SaveChangesAsync();
    }
}
