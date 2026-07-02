using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class MatchPredictionEntry
{
    public string DisplayName { get; set; } = "";
    public char? PredOutcome { get; set; }
    public MatchDecidedIn? PredInstance { get; set; }
    public int PtsResult { get; set; }
    public int PtsInstance { get; set; }
    public int Points => PtsResult + PtsInstance;
    public bool HasPrediction => PredOutcome.HasValue;
}

public class MatchPredictionsSummary
{
    public Match Match { get; set; } = null!;
    public List<MatchPredictionEntry> Entries { get; set; } = [];
    public int TotalCorrect => Entries.Count(e => e.PtsResult > 0);
    public int TotalWithPrediction => Entries.Count(e => e.HasPrediction);
    public double AveragePoints => TotalWithPrediction > 0
        ? Entries.Where(e => e.HasPrediction).Average(e => e.Points)
        : 0;
}

public class MatchPredictionsService(IDbContextFactory<QuinielaDbContext> dbFactory)
{
    public async Task<MatchPredictionsSummary> GetForMatchAsync(int matchId, int poolId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var match = await db.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .FirstOrDefaultAsync(m => m.Id == matchId)
            ?? throw new KeyNotFoundException($"Match {matchId} not found.");

        var members = await db.PoolMembers
            .Include(pm => pm.User)
            .Where(pm => pm.PoolId == poolId)
            .ToListAsync();

        var preds = await db.Predictions
            .Where(p => p.MatchId == matchId && p.PoolId == poolId)
            .ToDictionaryAsync(p => p.UserId);

        var entries = members.Select(m =>
        {
            preds.TryGetValue(m.UserId, out var pred);
            return new MatchPredictionEntry
            {
                DisplayName  = m.User.DisplayName,
                PredOutcome  = pred?.PredOutcome,
                PredInstance = pred?.PredInstance,
                PtsResult    = pred?.PtsResult ?? 0,
                PtsInstance  = pred?.PtsInstance ?? 0,
            };
        })
        .OrderByDescending(e => e.Points)
        .ThenBy(e => e.DisplayName)
        .ToList();

        return new MatchPredictionsSummary { Match = match, Entries = entries };
    }
}
