using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class NotificationCheckService(
    IDbContextFactory<QuinielaDbContext> dbFactory,
    PushNotificationService pushService,
    DailyAwardService dailyAwardService)
{
    private const string ReminderType = "MatchReminder";
    private const string MatchStartedType = "MatchStarted";
    private const string DailySummaryType = "DailySummary";
    private const string DailyAwardType = "DailyAward";

    // Misma zona fija que DailySummaryService: sin DST desde 2022, conversión estable.
    private static readonly TimeZoneInfo Tz =
        TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    public async Task CheckAndNotifyAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var now = DateTime.UtcNow;
        await CheckUpcomingMatchesAsync(db, now);   // N2
        await CheckStartedMatchesAsync(db, now);    // N8
        await CheckDailyAwardsAsync(db, now);       // N10 (21:30, antes del resumen de las 22:00)
        await CheckDailySummaryAsync(db, now);      // N9
    }

    private async Task CheckUpcomingMatchesAsync(QuinielaDbContext db, DateTime now)
    {
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

    private async Task CheckStartedMatchesAsync(QuinielaDbContext db, DateTime now)
    {
        // Ventana [now - 15min, now]: cubre el intervalo de 10 min entre pings con margen por cold start
        var from = now.AddMinutes(-15);

        var startedMatches = await db.Matches
            .Where(m => m.KickoffUtc <= now && m.KickoffUtc > from
                        && m.Status == MatchStatus.Programado
                        && m.HomeTeamId != null && m.AwayTeamId != null)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .ToListAsync();

        if (startedMatches.Count == 0) return;

        var matchIds = startedMatches.Select(m => m.Id).ToList();

        var poolsByUser = (await db.PoolMembers
            .Select(pm => new { pm.UserId, pm.PoolId })
            .ToListAsync())
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.PoolId).ToList());

        var notifiedByMatch = (await db.NotificationLogs
            .Where(n => n.Type == MatchStartedType && matchIds.Contains(n.MatchId))
            .Select(n => new { n.MatchId, n.UserId })
            .ToListAsync())
            .GroupBy(n => n.MatchId)
            .ToDictionary(g => g.Key, g => g.Select(n => n.UserId).ToHashSet());

        foreach (var match in startedMatches)
        {
            var alreadyNotified = notifiedByMatch.GetValueOrDefault(match.Id, []);
            var label = $"{match.HomeTeam?.ShortCode ?? "?"} vs {match.AwayTeam?.ShortCode ?? "?"}";

            foreach (var (userId, poolIds) in poolsByUser)
            {
                if (alreadyNotified.Contains(userId)) continue;

                var url = poolIds.Count == 1 ? $"/pools/{poolIds[0]}/predictions" : "/pools";
                await pushService.SendAsync(userId, $"🔴 ¡Arrancó {label}!",
                    "El partido está en juego — mira los pronósticos de tu sala.", url);

                db.NotificationLogs.Add(new NotificationLog { UserId = userId, MatchId = match.Id, Type = MatchStartedType, SentAt = now });
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// N10: mejor/peor del día a las 21:30 CDMX (antes del resumen N9 de las 22:00). El primer
    /// ping después de las 21:30 dispara el envío; la dedup en NotificationLog (una fila por
    /// usuario y día, MatchId = último partido finalizado del día) bloquea los siguientes.
    /// Es evento de sala: en cada sala con premio (DailyAwardService.GetForDayAsync != null),
    /// el mejor y el peor reciben su versión personal y el resto el anuncio con nombres.
    /// Salas sin premio (todos empatados) no reciben nada. Resultados capturados después del
    /// envío no provocan reenvío ni retractación — la vitrina on-demand es la verdad.
    /// </summary>
    private async Task CheckDailyAwardsAsync(QuinielaDbContext db, DateTime now)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, Tz);
        if (localNow.Hour < 21 || (localNow.Hour == 21 && localNow.Minute < 30)) return;

        var day = DateOnly.FromDateTime(localNow);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Tz);
        var dayEndUtc = dayStartUtc.AddDays(1);

        var dayMatches = await db.Matches
            .Where(m => m.Status == MatchStatus.Finalizado
                        && m.KickoffUtc >= dayStartUtc && m.KickoffUtc < dayEndUtc)
            .Select(m => new { m.Id, m.KickoffUtc })
            .ToListAsync();

        if (dayMatches.Count == 0) return;

        var lastMatchId = dayMatches.OrderByDescending(m => m.KickoffUtc).First().Id;

        // Dedup por día local vía join a Match (mismo patrón que N9)
        var alreadyNotified = (await db.NotificationLogs
            .Where(n => n.Type == DailyAwardType
                        && n.Match.KickoffUtc >= dayStartUtc && n.Match.KickoffUtc < dayEndUtc)
            .Select(n => n.UserId)
            .ToListAsync())
            .ToHashSet();

        var members = await db.PoolMembers
            .Select(pm => new { pm.PoolId, pm.UserId, PoolName = pm.Pool.Name, pm.User.DisplayName })
            .ToListAsync();

        // Premio del día por sala; salas sin premio (null) se saltan por completo
        var awardsByPool = new Dictionary<int, DailyAwardService.DayAwards>();
        foreach (var poolId in members.Select(m => m.PoolId).Distinct())
        {
            var awards = await dailyAwardService.GetForDayAsync(poolId, day);
            if (awards is not null) awardsByPool[poolId] = awards;
        }

        if (awardsByPool.Count == 0) return;

        // Nombres para el anuncio, unidos con " y " cuando hay empate
        var bestNamesByPool = awardsByPool.ToDictionary(
            kv => kv.Key,
            kv => string.Join(" y ", members
                .Where(m => m.PoolId == kv.Key && kv.Value.BestUserIds.Contains(m.UserId))
                .Select(m => m.DisplayName).OrderBy(n => n)));
        var worstNamesByPool = awardsByPool.ToDictionary(
            kv => kv.Key,
            kv => string.Join(" y ", members
                .Where(m => m.PoolId == kv.Key && kv.Value.WorstUserIds.Contains(m.UserId))
                .Select(m => m.DisplayName).OrderBy(n => n)));

        foreach (var userGroup in members.GroupBy(m => m.UserId))
        {
            if (alreadyNotified.Contains(userGroup.Key)) continue;

            // Todas las salas del usuario se envían en la misma corrida antes de registrar el log
            var sentAny = false;
            foreach (var member in userGroup)
            {
                if (!awardsByPool.TryGetValue(member.PoolId, out var awards)) continue;

                var url = $"/pools/{member.PoolId}/achievements";

                if (awards.BestUserIds.Contains(member.UserId))
                {
                    await pushService.SendAsync(member.UserId,
                        "🔮 Hoy amaneciste brujo",
                        $"Fuiste el mejor del día en \"{member.PoolName}\".\nNadie te llegó ni a los talones.\nPasa a recoger tu medalla 🏅",
                        url);
                }
                else if (awards.WorstUserIds.Contains(member.UserId))
                {
                    await pushService.SendAsync(member.UserId,
                        "🥴 Ouch… el peor del día",
                        $"Nadie pronosticó peor que tú hoy en \"{member.PoolName}\".\nUna moneda al aire lo hace mejor.\nMedalla de plomo a tu vitrina 🏅",
                        url);
                }
                else
                {
                    var bestLine = awards.BestUserIds.Count > 1
                        ? $"{bestNamesByPool[member.PoolId]} son los mejores del día 👑"
                        : $"{bestNamesByPool[member.PoolId]} es el mejor del día 👑";
                    await pushService.SendAsync(member.UserId,
                        $"📰 Última hora en \"{member.PoolName}\"",
                        $"{bestLine}\n{worstNamesByPool[member.PoolId]}… mejor ni preguntes 💀",
                        url);
                }

                sentAny = true;
            }

            if (sentAny)
            {
                db.NotificationLogs.Add(new NotificationLog
                {
                    UserId = userGroup.Key,
                    MatchId = lastMatchId,
                    Type = DailyAwardType,
                    SentAt = now,
                });
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// N9: resumen diario a las 22:00 CDMX. El primer ping después de las 22:00 dispara el envío;
    /// los siguientes quedan bloqueados por la dedup en NotificationLog (una fila por usuario y día,
    /// con MatchId = último partido finalizado del día). Solo se envía en días con al menos un
    /// partido finalizado. Es evento de sala: una notificación por sala, con nombre de sala.
    /// </summary>
    private async Task CheckDailySummaryAsync(QuinielaDbContext db, DateTime now)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, Tz);
        if (localNow.Hour < 22) return;

        var day = DateOnly.FromDateTime(localNow);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Tz);
        var dayEndUtc = dayStartUtc.AddDays(1);

        var dayMatches = await db.Matches
            .Where(m => m.Status == MatchStatus.Finalizado
                        && m.KickoffUtc >= dayStartUtc && m.KickoffUtc < dayEndUtc)
            .Select(m => new { m.Id, m.KickoffUtc })
            .ToListAsync();

        if (dayMatches.Count == 0) return;

        var dayMatchIds = dayMatches.Select(m => m.Id).ToList();
        var lastMatchId = dayMatches.OrderByDescending(m => m.KickoffUtc).First().Id;

        // Dedup por día local vía join a Match (no por MatchId exacto): si el usuario ya tiene
        // un log DailySummary cuyo partido cae en este día, ya recibió el resumen de hoy.
        var alreadyNotified = (await db.NotificationLogs
            .Where(n => n.Type == DailySummaryType
                        && n.Match.KickoffUtc >= dayStartUtc && n.Match.KickoffUtc < dayEndUtc)
            .Select(n => n.UserId)
            .ToListAsync())
            .ToHashSet();

        var members = await db.PoolMembers
            .Select(pm => new { pm.PoolId, pm.UserId, PoolName = pm.Pool.Name })
            .ToListAsync();

        // Puntos del día por (sala, usuario); Count distingue "0 pts" de "no pronosticó"
        var dayPoints = (await db.Predictions
            .Where(p => dayMatchIds.Contains(p.MatchId))
            .GroupBy(p => new { p.PoolId, p.UserId })
            .Select(g => new { g.Key.PoolId, g.Key.UserId, Points = g.Sum(p => p.Points), Count = g.Count() })
            .ToListAsync())
            .ToDictionary(x => (x.PoolId, x.UserId), x => (x.Points, x.Count));

        // Posiciones: actual = último snapshot antes del fin del día; previa = último antes del
        // inicio del día (mismo criterio que N3/Módulo L, reutilizando StandingsSnapshots)
        var snapshots = await db.StandingsSnapshots
            .Where(s => s.Match.KickoffUtc < dayEndUtc)
            .Select(s => new { s.PoolId, s.UserId, s.Position, s.Match.KickoffUtc })
            .ToListAsync();

        var currentPositions = snapshots
            .GroupBy(s => (s.PoolId, s.UserId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.KickoffUtc).First().Position);

        var previousPositions = snapshots
            .Where(s => s.KickoffUtc < dayStartUtc)
            .GroupBy(s => (s.PoolId, s.UserId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.KickoffUtc).First().Position);

        var title = $"📅 Tu resumen del {day.ToDateTime(TimeOnly.MinValue).ToString("d 'de' MMMM", new CultureInfo("es-MX"))}";
        var dateParam = day.ToString("yyyy-MM-dd");

        foreach (var userGroup in members.GroupBy(m => m.UserId))
        {
            if (alreadyNotified.Contains(userGroup.Key)) continue;

            // Todas las salas del usuario se envían en la misma corrida antes de registrar el log
            foreach (var member in userGroup)
            {
                var (points, predCount) = dayPoints.GetValueOrDefault((member.PoolId, member.UserId));
                var ptsPart = predCount > 0
                    ? $"{(points > 0 ? $"+{points}" : "0")} pts hoy"
                    : "Hoy no pronosticaste";

                string? posPart = null;
                if (currentPositions.TryGetValue((member.PoolId, member.UserId), out var pos))
                {
                    posPart = previousPositions.TryGetValue((member.PoolId, member.UserId), out var prev) && prev != pos
                        ? (pos < prev ? $"⬆️ Subiste al {pos}° lugar" : $"⬇️ Bajaste al {pos}° lugar")
                        : $"Sigues en {pos}° lugar";
                }

                var body = posPart is null ? ptsPart : $"{ptsPart} · {posPart}";
                await pushService.SendAsync(member.UserId, title,
                    $"{body}\nSala: {member.PoolName}",
                    $"/pools/{member.PoolId}/daily-summary?date={dateParam}");
            }

            db.NotificationLogs.Add(new NotificationLog
            {
                UserId = userGroup.Key,
                MatchId = lastMatchId,
                Type = DailySummaryType,
                SentAt = now,
            });
        }

        await db.SaveChangesAsync();
    }
}
