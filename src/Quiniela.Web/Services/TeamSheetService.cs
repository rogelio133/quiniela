using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class TeamSheetMatchEntry
{
    public Match Match { get; set; } = null!;
    public Prediction? UserPrediction { get; set; }
}

public class TeamSheetData
{
    public Team Team { get; set; } = null!;
    public GroupStanding? GroupPosition { get; set; }
    public int GroupPositionRank { get; set; }
    public List<TeamSheetMatchEntry> PreviousMatches { get; set; } = [];
    public List<Match> UpcomingMatches { get; set; } = [];
    public string? DatoCurioso { get; set; }
    public string? DirectorTecnico { get; set; }
    public List<Jugador> Jugadores { get; set; } = [];
    public List<HistorialMundial> HistorialMundiales { get; set; } = [];
}

public class TeamSheetService(QuinielaDbContext db)
{
    public async Task<TeamSheetData> GetTeamSheetAsync(int teamId, int userId, int poolId)
    {
        var team = await db.Teams
            .Include(t => t.Jugadores.OrderBy(j => j.Posicion).ThenBy(j => j.Nombre))
            .Include(t => t.HistorialMundiales)
            .FirstOrDefaultAsync(t => t.Id == teamId)
            ?? throw new KeyNotFoundException($"Team {teamId} not found");

        var allMatches = await db.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId)
            .OrderBy(m => m.KickoffUtc)
            .ToListAsync();

        var previous = allMatches.Where(m => m.Status == MatchStatus.Finalizado).ToList();
        var upcoming = allMatches.Where(m => m.Status == MatchStatus.Programado).ToList();

        // Load user predictions for previous matches in this pool
        var prevIds = previous.Select(m => m.Id).ToList();
        var predMap = await db.Predictions
            .Where(p => p.UserId == userId && p.PoolId == poolId && prevIds.Contains(p.MatchId))
            .ToDictionaryAsync(p => p.MatchId);

        var entries = previous.Select(m => new TeamSheetMatchEntry
        {
            Match          = m,
            UserPrediction = predMap.TryGetValue(m.Id, out var p) ? p : null
        }).ToList();

        // Compute group position for this team
        GroupStanding? position = null;
        int rank = 0;
        if (team.GroupCode.HasValue)
        {
            var groupFinished = await db.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Where(m => m.Stage == MatchStage.Grupos
                         && m.GroupCode == team.GroupCode.Value
                         && m.Status == MatchStatus.Finalizado)
                .ToListAsync();

            var groupTeams = await db.Teams
                .Where(t => t.GroupCode == team.GroupCode.Value)
                .ToListAsync();

            var standings = GroupStandingsService.ComputeStandings(groupTeams, groupFinished);
            var idx       = standings.FindIndex(s => s.Team.Id == teamId);
            if (idx >= 0) { position = standings[idx]; rank = idx + 1; }
        }

        return new TeamSheetData
        {
            Team               = team,
            GroupPosition      = position,
            GroupPositionRank  = rank,
            PreviousMatches    = entries,
            UpcomingMatches    = upcoming,
            DatoCurioso        = team.DatoCurioso,
            DirectorTecnico    = team.DirectorTecnico,
            Jugadores          = team.Jugadores.ToList(),
            HistorialMundiales = team.HistorialMundiales.ToList(),
        };
    }
}
