using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class KnockoutService(QuinielaDbContext db)
{
    public async Task<List<Match>> GetMatchesByStageAsync(MatchStage stage)
    {
        return await db.Matches
            .Where(m => m.Stage == stage)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderBy(m => m.BracketOrder)
            .ThenBy(m => m.KickoffUtc)
            .ToListAsync();
    }

    public async Task<List<Match>> GetPlaceholderMatchesAsync()
    {
        return await db.Matches
            .Where(m => m.Stage != MatchStage.Grupos && (m.HomeTeamId == null || m.AwayTeamId == null))
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderBy(m => m.Stage)
            .ThenBy(m => m.BracketOrder)
            .ToListAsync();
    }

    public async Task<List<Team>> GetAllTeamsAsync() =>
        await db.Teams.OrderBy(t => t.Name).ToListAsync();

    public async Task<(bool Success, string? Error)> AssignTeamsAsync(int matchId, int? homeTeamId, int? awayTeamId)
    {
        var match = await db.Matches.FindAsync(matchId);
        if (match is null) return (false, "Partido no encontrado.");
        if (match.Stage == MatchStage.Grupos) return (false, "No aplica a partidos de grupos.");

        if (homeTeamId is not null)
        {
            bool homeTaken = await db.Matches.AnyAsync(m =>
                m.Id != matchId && m.Stage == match.Stage &&
                (m.HomeTeamId == homeTeamId || m.AwayTeamId == homeTeamId));
            if (homeTaken) return (false, "El equipo local ya está asignado a otro partido de esta ronda.");
        }

        if (awayTeamId is not null)
        {
            bool awayTaken = await db.Matches.AnyAsync(m =>
                m.Id != matchId && m.Stage == match.Stage &&
                (m.HomeTeamId == awayTeamId || m.AwayTeamId == awayTeamId));
            if (awayTaken) return (false, "El equipo visitante ya está asignado a otro partido de esta ronda.");
        }

        match.HomeTeamId = homeTeamId;
        match.AwayTeamId = awayTeamId;
        await db.SaveChangesAsync();
        return (true, null);
    }
}
