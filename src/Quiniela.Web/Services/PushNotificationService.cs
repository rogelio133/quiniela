using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using WebPush;
using PushSubscriptionEntity = Quiniela.Data.Entities.PushSubscription;

namespace Quiniela.Web.Services;

public class PushNotificationService(IDbContextFactory<QuinielaDbContext> dbFactory, IConfiguration config)
{
    private readonly WebPushClient _client = new();

    public async Task SendAsync(int userId, string title, string body, string? url = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var subscriptions = await db.PushSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync();

        if (subscriptions.Count == 0) return;

        var payload = JsonSerializer.Serialize(new { title, body, url = url ?? "/" });
        var vapidDetails = new VapidDetails(
            config["Push:Subject"],
            config["Push:VapidPublicKey"],
            config["Push:VapidPrivateKey"]);

        foreach (var sub in subscriptions)
        {
            var pushSubscription = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
            try
            {
                await _client.SendNotificationAsync(pushSubscription, payload, vapidDetails);
            }
            catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone)
            {
                await RemoveSubscriptionAsync(sub.Endpoint);
            }
            catch
            {
                // best-effort: los envíos push nunca deben propagar excepciones al llamador
            }
        }
    }

    public async Task UpsertSubscriptionAsync(int userId, string endpoint, string p256dh, string auth)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (existing is not null)
        {
            existing.UserId = userId;
            existing.P256dh = p256dh;
            existing.Auth = auth;
        }
        else
        {
            db.PushSubscriptions.Add(new PushSubscriptionEntity
            {
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
            });
        }
        await db.SaveChangesAsync();
    }

    public async Task RemoveSubscriptionAsync(string endpoint)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (existing is not null)
        {
            db.PushSubscriptions.Remove(existing);
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> HasSubscriptionAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.PushSubscriptions.AnyAsync(s => s.UserId == userId);
    }
}
