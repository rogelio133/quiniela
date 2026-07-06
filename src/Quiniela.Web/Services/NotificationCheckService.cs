using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class NotificationCheckService(IDbContextFactory<QuinielaDbContext> dbFactory, PushNotificationService pushService)
{
    private const string ReminderType = "MatchReminder";

    public async Task CheckAndNotifyAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var now = DateTime.UtcNow;
        var from = now.AddMinutes(50);
        var to = now.AddMinutes(70);

        var upcomingMatches = await db.Matches
            .Where(m => m.KickoffUtc >= from && m.KickoffUtc <= to && m.HomeTeamId != null && m.AwayTeamId != null)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .ToListAsync();

        if (upcomingMatches.Count == 0) return;

        var matchIds = upcomingMatches.Select(m => m.Id).ToList();

        var poolsByUser = (await db.PoolMembers
            .Select(pm => new { pm.UserId, pm.PoolId })
            .ToListAsync())
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.PoolId).ToList());

        var predictedSet = (await db.Predictions
            .Where(p => matchIds.Contains(p.MatchId))
            .Select(p => new { p.MatchId, p.UserId, p.PoolId })
            .ToListAsync())
            .Select(p => (p.MatchId, p.UserId, p.PoolId))
            .ToHashSet();

        var notifiedByMatch = (await db.NotificationLogs
            .Where(n => n.Type == ReminderType && matchIds.Contains(n.MatchId))
            .Select(n => new { n.MatchId, n.UserId })
            .ToListAsync())
            .GroupBy(n => n.MatchId)
            .ToDictionary(g => g.Key, g => g.Select(n => n.UserId).ToHashSet());

        var pendingByUser = new Dictionary<int, List<Match>>();

        foreach (var match in upcomingMatches)
        {
            var alreadyNotified = notifiedByMatch.GetValueOrDefault(match.Id, []);

            foreach (var (userId, poolIds) in poolsByUser)
            {
                if (alreadyNotified.Contains(userId)) continue;
                if (!poolIds.Any(poolId => !predictedSet.Contains((match.Id, userId, poolId)))) continue;

                if (!pendingByUser.TryGetValue(userId, out var list))
                    pendingByUser[userId] = list = [];
                list.Add(match);
            }
        }

        foreach (var (userId, matches) in pendingByUser)
        {
            var poolIds = poolsByUser[userId];
            var url = poolIds.Count == 1 ? $"/pools/{poolIds[0]}/predictions" : "/pools";

            if (matches.Count == 1)
            {
                var m = matches[0];
                var label = $"{m.HomeTeam?.ShortCode ?? "?"} vs {m.AwayTeam?.ShortCode ?? "?"}";
                await pushService.SendAsync(userId, $"⏰ Faltan 60 min — {label}", "Aún no has pronosticado este partido", url);
            }
            else
            {
                await pushService.SendAsync(userId, $"⏰ {matches.Count} partidos cierran pronto", "Entra antes de que arranquen", url);
            }

            foreach (var m in matches)
                db.NotificationLogs.Add(new NotificationLog { UserId = userId, MatchId = m.Id, Type = ReminderType, SentAt = now });
        }

        await db.SaveChangesAsync();
    }
}
