using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class PredictionService(IDbContextFactory<QuinielaDbContext> dbFactory)
{
    public record MatchWithPrediction(Match Match, Prediction? Prediction);

    public async Task<(Pool? Pool, bool IsMember)> GetPoolContextAsync(int poolId, int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var pool = await db.Pools.FindAsync(poolId);
        if (pool is null) return (null, false);
        bool isMember = await db.PoolMembers.AnyAsync(m => m.PoolId == poolId && m.UserId == userId);
        return (pool, isMember);
    }

    public async Task<List<MatchWithPrediction>> GetUpcomingMatchesAsync(int poolId, int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var now = DateTime.UtcNow;
        var matches = await db.Matches
            .Where(m => m.KickoffUtc > now && m.HomeTeamId != null && m.AwayTeamId != null)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderBy(m => m.KickoffUtc)
            .ToListAsync();

        return await AttachPredictionsAsync(db, matches, poolId, userId);
    }

    public async Task<List<MatchWithPrediction>> GetAllMatchesAsync(int poolId, int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var matches = await db.Matches
            .Where(m => m.HomeTeamId != null && m.AwayTeamId != null)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderBy(m => m.KickoffUtc)
            .ToListAsync();

        return await AttachPredictionsAsync(db, matches, poolId, userId);
    }

    public async Task<(bool Success, string? Error)> UpsertAsync(
        int userId, int poolId, int matchId, char outcome, MatchDecidedIn? instance)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var match = await db.Matches.FindAsync(matchId);
        if (match is null) return (false, "Partido no encontrado.");
        if (match.KickoffUtc <= DateTime.UtcNow) return (false, "El partido ya inició, no puedes modificar tu pronóstico.");

        bool isKnockout = match.Stage != MatchStage.Grupos;

        if (isKnockout)
        {
            if (outcome is not ('H' or 'A'))
                return (false, "En eliminatorias debes elegir quién avanza.");
            if (instance is null)
                return (false, "Selecciona la instancia (90 min, tiempo extra o penales).");
        }
        else if (outcome is not ('H' or 'D' or 'A'))
            return (false, "Resultado inválido.");

        bool isMember = await db.PoolMembers.AnyAsync(m => m.PoolId == poolId && m.UserId == userId);
        if (!isMember) return (false, "No eres miembro de esta sala.");

        var existing = await db.Predictions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PoolId == poolId && p.MatchId == matchId);

        var predInstance = isKnockout ? instance : null;
        var now = DateTime.UtcNow;
        if (existing is not null)
        {
            bool realChange = existing.PredOutcome != outcome || existing.PredInstance != predInstance;
            existing.PredOutcome = outcome;
            existing.PredInstance = predInstance;
            existing.UpdatedAt = now;

            if (realChange)
                db.PredictionHistories.Add(new PredictionHistory
                {
                    PredictionId = existing.Id,
                    PredOutcome = outcome,
                    PredInstance = predInstance,
                    ChangedAt = now
                });

            await db.SaveChangesAsync();
        }
        else
        {
            var prediction = new Prediction
            {
                UserId = userId,
                PoolId = poolId,
                MatchId = matchId,
                PredOutcome = outcome,
                PredInstance = predInstance,
                Points = 0,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Predictions.Add(prediction);
            await db.SaveChangesAsync(); // necesario para obtener prediction.Id antes de crear el historial

            db.PredictionHistories.Add(new PredictionHistory
            {
                PredictionId = prediction.Id,
                PredOutcome = outcome,
                PredInstance = predInstance,
                ChangedAt = now
            });
            await db.SaveChangesAsync();
        }

        return (true, null);
    }

    public async Task<int> GetPendingCountAsync(int userId, int poolId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var now = DateTime.UtcNow;

        var upcomingMatchIds = await db.Matches
            .Where(m => m.KickoffUtc > now && m.HomeTeamId != null && m.AwayTeamId != null)
            .Select(m => m.Id)
            .ToListAsync();

        if (upcomingMatchIds.Count == 0) return 0;

        var predictedCount = await db.Predictions
            .CountAsync(p => p.UserId == userId
                          && p.PoolId == poolId
                          && upcomingMatchIds.Contains(p.MatchId));

        return upcomingMatchIds.Count - predictedCount;
    }

    private static async Task<List<MatchWithPrediction>> AttachPredictionsAsync(
        QuinielaDbContext db, List<Match> matches, int poolId, int userId)
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
