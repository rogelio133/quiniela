using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class BracketRound
{
    public MatchStage Stage { get; set; }
    public string Label { get; set; } = "";
    public List<BracketMatch> Matches { get; set; } = [];
}

public class BracketMatch
{
    public Match Match { get; set; } = null!;
    public string HomeName { get; set; } = "";
    public string AwayName { get; set; } = "";
    public string HomeFlagCode { get; set; } = "";
    public string AwayFlagCode { get; set; } = "";
    public bool IsFinalized { get; set; }
    public char? WinnerSide { get; set; }  // 'H' or 'A'
}

public class BracketService(QuinielaDbContext db)
{
    private static readonly MatchStage[] KoStages =
    [
        MatchStage.Dieciseisavos, MatchStage.Octavos,
        MatchStage.Cuartos, MatchStage.Semifinal,
        MatchStage.TercerLugar, MatchStage.Final
    ];

    public async Task<List<BracketRound>> GetBracketAsync()
    {
        var matches = await db.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => KoStages.Contains(m.Stage))
            .OrderBy(m => m.Stage)
            .ThenBy(m => m.BracketOrder)
            .ToListAsync();

        return KoStages
            .Where(s => matches.Any(m => m.Stage == s))
            .Select(stage => new BracketRound
            {
                Stage = stage,
                Label = StageLabel(stage),
                Matches = matches
                    .Where(m => m.Stage == stage)
                    .Select(BuildBracketMatch)
                    .ToList()
            })
            .ToList();
    }

    private static BracketMatch BuildBracketMatch(Match m) => new()
    {
        Match = m,
        HomeName = m.HomeTeam?.ShortCode ?? m.HomeSlotLabel ?? "?",
        AwayName = m.AwayTeam?.ShortCode ?? m.AwaySlotLabel ?? "?",
        HomeFlagCode = m.HomeTeam?.FlagCode ?? "",
        AwayFlagCode = m.AwayTeam?.FlagCode ?? "",
        IsFinalized = m.Status == MatchStatus.Finalizado,
        WinnerSide = m.Status == MatchStatus.Finalizado && m.HomeScore.HasValue && m.AwayScore.HasValue
            ? (m.HomeScore > m.AwayScore ? 'H' : 'A')
            : null,
    };

    private static string StageLabel(MatchStage s) => s switch
    {
        MatchStage.Dieciseisavos => "16avos",
        MatchStage.Octavos       => "Octavos",
        MatchStage.Cuartos       => "Cuartos",
        MatchStage.Semifinal     => "Semis",
        MatchStage.TercerLugar   => "3er Lugar",
        MatchStage.Final         => "Final",
        _                        => s.ToString()
    };
}
