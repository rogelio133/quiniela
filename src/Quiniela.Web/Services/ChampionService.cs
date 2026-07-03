using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public enum ChampionWindowState { NotYetOpen, Open, Closed }

public class ChampionService(IDbContextFactory<QuinielaDbContext> dbFactory)
{
    private static readonly MatchStage[] KoStages =
    [
        MatchStage.Dieciseisavos, MatchStage.Octavos,
        MatchStage.Cuartos, MatchStage.Semifinal,
        MatchStage.TercerLugar, MatchStage.Final
    ];

    public async Task<ChampionWindowState> GetWindowStateAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await GetWindowStateAsync(db);
    }

    private static async Task<ChampionWindowState> GetWindowStateAsync(QuinielaDbContext db)
    {
        var dieciseisavos = await db.Matches
            .Where(m => m.Stage == MatchStage.Dieciseisavos)
            .ToListAsync();

        if (dieciseisavos.Count == 0 || dieciseisavos.Any(m => m.Status != MatchStatus.Finalizado))
            return ChampionWindowState.NotYetOpen;

        var firstOctavosKickoff = await db.Matches
            .Where(m => m.Stage == MatchStage.Octavos)
            .OrderBy(m => m.KickoffUtc)
            .Select(m => (DateTime?)m.KickoffUtc)
            .FirstOrDefaultAsync();

        return firstOctavosKickoff is null || DateTime.UtcNow < firstOctavosKickoff
            ? ChampionWindowState.Open
            : ChampionWindowState.Closed;
    }

    /// <summary>
    /// Equipos que siguen con vida en 16avos: ganaron su partido ya finalizado, o aún no lo juegan.
    /// Una vez que los 16avos terminaron, esta lista coincide exactamente con los 16 clasificados a Octavos.
    /// </summary>
    public async Task<List<Team>> GetEligibleTeamsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var dieciseisavos = await db.Matches
            .Where(m => m.Stage == MatchStage.Dieciseisavos)
            .ToListAsync();

        var eliminated = dieciseisavos
            .Where(m => m.Status == MatchStatus.Finalizado)
            .Select(m => m.HomeScore > m.AwayScore ? m.AwayTeamId : m.HomeTeamId)
            .Where(id => id != null)
            .Select(id => id!.Value)
            .ToHashSet();

        var allTeamIds = dieciseisavos
            .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
            .Where(id => id != null)
            .Select(id => id!.Value)
            .ToHashSet();

        return await db.Teams
            .Where(t => allTeamIds.Contains(t.Id) && !eliminated.Contains(t.Id))
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<ChampionPrediction?> GetMyPredictionAsync(int userId, int poolId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ChampionPredictions
            .Include(c => c.Team)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.PoolId == poolId);
    }

    public async Task<(bool Success, string? Error)> UpsertAsync(int userId, int poolId, int teamId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        if (await GetWindowStateAsync(db) != ChampionWindowState.Open)
            return (false, "La selección de campeón no está disponible en este momento.");

        bool isMember = await db.PoolMembers.AnyAsync(m => m.PoolId == poolId && m.UserId == userId);
        if (!isMember) return (false, "No eres miembro de esta sala.");

        var existing = await db.ChampionPredictions
            .FirstOrDefaultAsync(c => c.UserId == userId && c.PoolId == poolId);

        var now = DateTime.UtcNow;
        if (existing is not null)
        {
            existing.TeamId = teamId;
            existing.UpdatedAt = now;
        }
        else
        {
            db.ChampionPredictions.Add(new ChampionPrediction
            {
                UserId = userId,
                PoolId = poolId,
                TeamId = teamId,
                Points = 0,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// Un equipo está eliminado si perdió cualquier partido de fase eliminatoria ya finalizado.
    /// </summary>
    public async Task<bool> IsTeamEliminatedAsync(int teamId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Matches.AnyAsync(m =>
            KoStages.Contains(m.Stage) &&
            m.Status == MatchStatus.Finalizado &&
            ((m.HomeTeamId == teamId && m.HomeScore < m.AwayScore) ||
             (m.AwayTeamId == teamId && m.AwayScore < m.HomeScore)));
    }

    /// <summary>
    /// Pronóstico de campeón de cada miembro de la sala, para mostrar en Standings.
    /// </summary>
    public async Task<Dictionary<int, Team>> GetAllPredictionsForPoolAsync(int poolId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var preds = await db.ChampionPredictions
            .Where(c => c.PoolId == poolId)
            .Include(c => c.Team)
            .ToListAsync();
        return preds.ToDictionary(c => c.UserId, c => c.Team);
    }
}
