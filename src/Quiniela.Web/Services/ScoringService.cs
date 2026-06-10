using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class ScoringService(QuinielaDbContext db)
{
    public async Task RecalculateForMatchAsync(int matchId)
    {
        var match = await db.Matches.FindAsync(matchId);
        if (match is null || match.HomeScore is null || match.AwayScore is null)
            return;

        char realOutcome = match.HomeScore > match.AwayScore ? 'H'
                         : match.HomeScore < match.AwayScore ? 'A'
                         : 'D';

        bool isKnockout = match.Stage != MatchStage.Grupos;

        var predictions = await db.Predictions
            .Where(p => p.MatchId == matchId)
            .Include(p => p.Pool)
            .ToListAsync();

        foreach (var pred in predictions)
        {
            pred.Points = pred.PredOutcome == realOutcome
                ? pred.Pool.PtsCorrect + (isKnockout ? pred.Pool.PtsBonusKO : 0)
                : 0;
        }

        await db.SaveChangesAsync();
    }
}
