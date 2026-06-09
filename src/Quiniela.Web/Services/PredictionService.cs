using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class PredictionService(QuinielaDbContext db)
{
    public record MatchWithPrediction(Match Match, Prediction? Prediction);

    public async Task<(Pool? Pool, bool IsMember)> GetPoolContextAsync(int poolId, int userId)
    {
        var pool = await db.Pools.FindAsync(poolId);
        if (pool is null) return (null, false);
        bool isMember = await db.PoolMembers.AnyAsync(m => m.PoolId == poolId && m.UserId == userId);
        return (pool, isMember);
    }

    public async Task<List<MatchWithPrediction>> GetUpcomingMatchesAsync(int poolId, int userId)
    {
        var now = DateTime.UtcNow;
        var matches = await db.Matches
            .Where(m => m.KickoffUtc > now && m.HomeTeamId != null && m.AwayTeamId != null)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderBy(m => m.KickoffUtc)
            .ToListAsync();

        return await AttachPredictionsAsync(matches, poolId, userId);
    }

    public async Task<List<MatchWithPrediction>> GetAllMatchesAsync(int poolId, int userId)
    {
        var matches = await db.Matches
            .Where(m => m.HomeTeamId != null && m.AwayTeamId != null)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderBy(m => m.KickoffUtc)
            .ToListAsync();

        return await AttachPredictionsAsync(matches, poolId, userId);
    }

    public async Task<(bool Success, string? Error)> UpsertAsync(int userId, int poolId, int matchId, char outcome)
    {
        if (outcome is not ('H' or 'D' or 'A'))
            return (false, "Resultado inválido.");

        var match = await db.Matches.FindAsync(matchId);
        if (match is null) return (false, "Partido no encontrado.");
        if (match.KickoffUtc <= DateTime.UtcNow) return (false, "El partido ya inició, no puedes modificar tu pronóstico.");

        bool isMember = await db.PoolMembers.AnyAsync(m => m.PoolId == poolId && m.UserId == userId);
        if (!isMember) return (false, "No eres miembro de esta sala.");

        var existing = await db.Predictions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PoolId == poolId && p.MatchId == matchId);

        var now = DateTime.UtcNow;
        if (existing is not null)
        {
            existing.PredOutcome = outcome;
            existing.UpdatedAt = now;
        }
        else
        {
            db.Predictions.Add(new Prediction
            {
                UserId = userId,
                PoolId = poolId,
                MatchId = matchId,
                PredOutcome = outcome,
                Points = 0,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync();
        return (true, null);
    }

    private async Task<List<MatchWithPrediction>> AttachPredictionsAsync(List<Match> matches, int poolId, int userId)
    {
        var matchIds = matches.Select(m => m.Id).ToList();
        var preds = await db.Predictions
            .Where(p => p.PoolId == poolId && p.UserId == userId && matchIds.Contains(p.MatchId))
            .ToListAsync();
        var predsByMatchId = preds.ToDictionary(p => p.MatchId);

        return matches
            .Select(m => new MatchWithPrediction(m, predsByMatchId.GetValueOrDefault(m.Id)))
            .ToList();
    }
}
