using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class GroupStanding
{
    public Team Team { get; set; } = null!;
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDiff => GoalsFor - GoalsAgainst;
    public int Points   => Won * 3 + Drawn;
}

public record GroupResult(List<GroupStanding> Standings, int Played, int Total);

public class GroupStandingsService(QuinielaDbContext db)
{
    public async Task<Dictionary<char, GroupResult>> GetAllGroupStandingsAsync()
    {
        var matches = await db.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.Stage == MatchStage.Grupos && m.GroupCode != null)
            .ToListAsync();

        var teams = await db.Teams
            .Where(t => t.GroupCode != null)
            .ToListAsync();

        var groupCodes = teams
            .Select(t => t.GroupCode!.Value)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var result = new Dictionary<char, GroupResult>();

        foreach (var gc in groupCodes)
        {
            var groupTeams = teams.Where(t => t.GroupCode == gc).ToList();
            var finished   = matches.Where(m => m.GroupCode == gc && m.Status == MatchStatus.Finalizado).ToList();
            var total      = matches.Count(m => m.GroupCode == gc);

            var standings = ComputeStandings(groupTeams, finished);
            result[gc] = new GroupResult(standings, finished.Count, total);
        }

        return result;
    }

    public static List<GroupStanding> ComputeStandings(List<Team> teams, List<Match> finishedMatches)
    {
        return teams.Select(team =>
        {
            var s = new GroupStanding { Team = team };

            foreach (var m in finishedMatches.Where(m => m.HomeTeamId == team.Id))
            {
                s.Played++;
                s.GoalsFor     += m.HomeScore ?? 0;
                s.GoalsAgainst += m.AwayScore ?? 0;
                if      (m.HomeScore > m.AwayScore)  s.Won++;
                else if (m.HomeScore == m.AwayScore) s.Drawn++;
                else                                 s.Lost++;
            }

            foreach (var m in finishedMatches.Where(m => m.AwayTeamId == team.Id))
            {
                s.Played++;
                s.GoalsFor     += m.AwayScore ?? 0;
                s.GoalsAgainst += m.HomeScore ?? 0;
                if      (m.AwayScore > m.HomeScore)  s.Won++;
                else if (m.HomeScore == m.AwayScore) s.Drawn++;
                else                                 s.Lost++;
            }

            return s;
        })
        .OrderByDescending(s => s.Points)
        .ThenByDescending(s => s.GoalDiff)
        .ThenByDescending(s => s.GoalsFor)
        .ThenBy(s => s.Team.Name)
        .ToList();
    }
}
