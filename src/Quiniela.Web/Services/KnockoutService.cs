using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class KnockoutService(IDbContextFactory<QuinielaDbContext> dbFactory, PushNotificationService pushService)
{
    private const string KnockoutAssignedType = "KnockoutAssigned";

    public async Task<List<MatchStage>> GetStagesWithMatchesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Matches
            .Select(m => m.Stage)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();
    }

    public async Task<List<Match>> GetMatchesByStageAsync(MatchStage stage)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
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
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Matches
            .Where(m => m.Stage != MatchStage.Grupos && (m.HomeTeamId == null || m.AwayTeamId == null))
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderBy(m => m.Stage)
            .ThenBy(m => m.BracketOrder)
            .ToListAsync();
    }

    public async Task<List<Team>> GetAllTeamsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Teams.OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<(bool Success, string? Error)> AssignTeamsAsync(int matchId, int? homeTeamId, int? awayTeamId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

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

        bool wasComplete = match.HomeTeamId is not null && match.AwayTeamId is not null;

        match.HomeTeamId = homeTeamId;
        match.AwayTeamId = awayTeamId;
        await db.SaveChangesAsync();

        if (!wasComplete && homeTeamId is not null && awayTeamId is not null)
            await NotifyMatchAvailableAsync(db, match);

        return (true, null);
    }

    /// <summary>
    /// N6: avisa que un cruce KO ya tiene ambos equipos y se puede pronosticar. Se dispara solo
    /// cuando el partido pasa de incompleto a completo. Los partidos aplican a todas las salas,
    /// así que cada usuario recibe una sola notificación (con el nombre de su primera sala),
    /// deduplicada vía NotificationLog para no repetir si el admin reasigna el cruce.
    /// </summary>
    private async Task NotifyMatchAvailableAsync(QuinielaDbContext db, Match match)
    {
        var homeTeam = await db.Teams.FindAsync(match.HomeTeamId);
        var awayTeam = await db.Teams.FindAsync(match.AwayTeamId);
        var body = $"{homeTeam?.ShortCode ?? "?"} vs. {awayTeam?.ShortCode ?? "?"} — {StageLabel(match.Stage)}";

        var members = await db.PoolMembers
            .OrderBy(pm => pm.PoolId)
            .Select(pm => new { pm.UserId, pm.PoolId, pm.Pool.Name })
            .ToListAsync();

        var alreadyNotified = await db.NotificationLogs
            .Where(n => n.MatchId == match.Id && n.Type == KnockoutAssignedType)
            .Select(n => n.UserId)
            .ToHashSetAsync();

        var now = DateTime.UtcNow;

        foreach (var group in members.GroupBy(m => m.UserId))
        {
            if (alreadyNotified.Contains(group.Key)) continue;

            var first = group.First();
            await pushService.SendAsync(group.Key,
                "🆕 Nuevo cruce disponible",
                $"{body}\nSala: {first.Name}",
                $"/pools/{first.PoolId}/predictions");

            db.NotificationLogs.Add(new NotificationLog
            {
                UserId = group.Key,
                MatchId = match.Id,
                Type = KnockoutAssignedType,
                SentAt = now,
            });
        }

        await db.SaveChangesAsync();
    }

    private static string StageLabel(MatchStage stage) => stage switch
    {
        MatchStage.Dieciseisavos => "Dieciseisavos",
        MatchStage.Octavos       => "Octavos de Final",
        MatchStage.Cuartos       => "Cuartos de Final",
        MatchStage.Semifinal     => "Semifinal",
        MatchStage.TercerLugar   => "Tercer Lugar",
        MatchStage.Final         => "Final",
        _                        => stage.ToString()
    };
}
