using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public record DayPoints(DateOnly Date, int Points);

public class PlayerStats
{
    public int TotalPoints { get; set; }
    public int Position { get; set; }
    public int TotalMembers { get; set; }

    public int TotalPredictions { get; set; }
    public int CorrectResults { get; set; }
    public int CorrectInstances { get; set; }
    public int GroupPredictions { get; set; }
    public int GroupCorrect { get; set; }
    public int KoPredictions { get; set; }
    public int KoCorrect { get; set; }

    public int BestStreak { get; set; }
    public int CurrentStreak { get; set; }

    public List<DayPoints> PointsByDay { get; set; } = [];
}

public class PlayerStatsService(IDbContextFactory<QuinielaDbContext> dbFactory, StandingsService standingsService)
{
    public async Task<PlayerStats> GetAsync(int userId, int poolId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var predictions = await db.Predictions
            .Include(p => p.Match)
            .Where(p => p.UserId == userId && p.PoolId == poolId
                     && p.Match.Status == MatchStatus.Finalizado)
            .OrderBy(p => p.Match.KickoffUtc)
            .ToListAsync();

        var standings = await standingsService.GetStandingsAsync(poolId);
        var myRow = standings.FirstOrDefault(s => s.UserId == userId);

        int bestStreak = 0, streak = 0;
        foreach (var p in predictions)
        {
            if (p.PtsResult > 0) { streak++; bestStreak = Math.Max(bestStreak, streak); }
            else streak = 0;
        }
        int currentStreak = streak;

        var byDay = predictions
            .GroupBy(p => DateOnly.FromDateTime(p.Match.KickoffUtc))
            .Select(g => new DayPoints(g.Key, g.Sum(p => p.Points)))
            .OrderBy(d => d.Date)
            .ToList();

        bool IsKo(Prediction p) => p.Match.Stage != MatchStage.Grupos;

        return new PlayerStats
        {
            TotalPoints      = myRow?.TotalPoints ?? 0,
            Position         = myRow is not null ? standings.IndexOf(myRow) + 1 : 0,
            TotalMembers     = standings.Count,
            TotalPredictions = predictions.Count,
            CorrectResults   = predictions.Count(p => p.PtsResult > 0),
            CorrectInstances = predictions.Count(p => p.PtsInstance > 0),
            GroupPredictions = predictions.Count(p => !IsKo(p)),
            GroupCorrect     = predictions.Count(p => !IsKo(p) && p.PtsResult > 0),
            KoPredictions    = predictions.Count(IsKo),
            KoCorrect        = predictions.Count(p => IsKo(p) && p.PtsResult > 0),
            BestStreak       = bestStreak,
            CurrentStreak    = currentStreak,
            PointsByDay      = byDay,
        };
    }
}
