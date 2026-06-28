using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class AdminService(QuinielaDbContext db, ScoringService scoringService)
{
    /// <summary>
    /// Matches that have started (KickoffUtc &lt;= now) but whose result hasn't been entered yet.
    /// </summary>
    public async Task<List<Match>> GetPendingResultsAsync()
    {
        var now = DateTime.UtcNow;
        return await db.Matches
            .Where(m => m.KickoffUtc <= now
                     && m.Status == MatchStatus.Programado
                     && m.HomeTeamId != null
                     && m.AwayTeamId != null)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderBy(m => m.KickoffUtc)
            .ToListAsync();
    }

    public async Task<List<Match>> GetFinalizedMatchesAsync()
    {
        return await db.Matches
            .Where(m => m.Status == MatchStatus.Finalizado)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderByDescending(m => m.KickoffUtc)
            .ToListAsync();
    }

    public async Task<(bool Success, string? Error)> SaveResultAsync(
        int matchId, int homeScore, int awayScore, MatchDecidedIn? decidedIn)
    {
        if (homeScore < 0 || awayScore < 0)
            return (false, "Los goles no pueden ser negativos.");

        var match = await db.Matches.FindAsync(matchId);
        if (match is null)
            return (false, "Partido no encontrado.");

        bool isKnockout = match.Stage != MatchStage.Grupos;

        if (isKnockout)
        {
            if (decidedIn is null)
                return (false, "Indica la instancia (90', tiempo extra o penales).");
            if (homeScore == awayScore)
                return (false, "En eliminatorias el marcador global no puede ser empate (incluye penales).");
        }

        match.HomeScore = homeScore;
        match.AwayScore = awayScore;
        match.DecidedIn = isKnockout ? decidedIn : null;
        match.Status = MatchStatus.Finalizado;
        await db.SaveChangesAsync();

        await scoringService.RecalculateForMatchAsync(matchId);
        return (true, null);
    }
}
