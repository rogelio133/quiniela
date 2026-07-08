using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class DailySummaryService(IDbContextFactory<QuinielaDbContext> dbFactory)
{
    // America/Mexico_City no observa DST desde 2022 (UTC-6 fijo), así que la
    // conversión DateOnly → rango UTC es estable. Misma zona fija que usan
    // MyPredictions/Predictions/MatchCard para agrupar partidos por fecha.
    private static readonly TimeZoneInfo Tz =
        TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    public record DailyRow(
        Match Match,
        char? PredOutcome,               // null = sin pronóstico
        MatchDecidedIn? PredInstance,
        int PtsResult,
        int PtsInstance)
    {
        public int Points => PtsResult + PtsInstance;
    }

    // Fila extra del día de la Final: pronóstico de campeón del usuario y sus puntos.
    public record ChampionRow(string TeamName, string FlagCode, string? ShortCode, int Points);

    public record DayLeader(int UserId, string DisplayName, string? ProfilePicturePath, int Points);

    public record DailySummary(
        DateOnly Date,
        List<DailyRow> Rows,
        int DayPoints,
        int? Position,                   // null si aún no hay snapshot ese día
        int? PreviousPosition,           // null si es el primer día con datos
        int TotalMembers,
        List<DayLeader> DayLeaders,      // "El mejor del día" (varios si hay empate; vacío si nadie sumó)
        List<DateOnly> AvailableDays,    // días con >= 1 partido finalizado (para el selector)
        ChampionRow? Champion);          // solo el día de la Final, si el usuario pronosticó campeón

    /// <summary>
    /// Resumen del día <paramref name="date"/> para el usuario en la sala. Si la fecha es null
    /// o no tiene partidos finalizados, cae al día más reciente con partidos. Devuelve null
    /// si el torneo no tiene ningún partido finalizado todavía.
    /// </summary>
    public async Task<DailySummary?> GetAsync(int poolId, int userId, DateOnly? date)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // 1) Días disponibles: partidos finalizados agrupados por fecha local CDMX
        //    (conversión en memoria — son ~100 partidos máximo, trivial)
        var finalized = await db.Matches
            .Where(m => m.Status == MatchStatus.Finalizado)
            .Select(m => new { m.Id, m.KickoffUtc })
            .ToListAsync();

        var availableDays = finalized
            .Select(m => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(m.KickoffUtc, Tz)))
            .Distinct().OrderBy(d => d).ToList();

        if (availableDays.Count == 0) return null;

        // 2) Fecha efectiva: la pedida si tiene partidos, si no la más reciente
        var day = date is not null && availableDays.Contains(date.Value)
            ? date.Value
            : availableDays[^1];

        var (dayStartUtc, dayEndUtc) = DayRangeUtc(day);

        // 3) Partidos del día + predicción del usuario en esta sala (left join)
        var matches = await db.Matches
            .Where(m => m.Status == MatchStatus.Finalizado
                        && m.KickoffUtc >= dayStartUtc && m.KickoffUtc < dayEndUtc)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderBy(m => m.KickoffUtc)
            .ToListAsync();

        var matchIds = matches.Select(m => m.Id).ToList();

        var myPredictions = await db.Predictions
            .Where(p => p.PoolId == poolId && p.UserId == userId && matchIds.Contains(p.MatchId))
            .ToDictionaryAsync(p => p.MatchId);

        var rows = matches
            .Select(m => myPredictions.TryGetValue(m.Id, out var p)
                ? new DailyRow(m, p.PredOutcome, p.PredInstance, p.PtsResult, p.PtsInstance)
                : new DailyRow(m, null, null, 0, 0))
            .ToList();

        // 4) Posición al cierre del día / 5) al cierre del día anterior con partidos
        var position = await SnapshotPositionAsync(db, poolId, userId, dayEndUtc);
        var previousPosition = await SnapshotPositionAsync(db, poolId, userId, dayStartUtc);

        var totalMembers = await db.PoolMembers.CountAsync(pm => pm.PoolId == poolId);

        // 6) Día de la Final: fila extra con el pronóstico de campeón del usuario, y
        //    puntos de campeón por usuario para incluirlos en los totales del día
        var isFinalDay = matches.Any(m => m.Stage == MatchStage.Final);
        ChampionRow? champion = null;
        var championPointsByUser = new Dictionary<int, int>();
        if (isFinalDay)
        {
            champion = await db.ChampionPredictions
                .Where(c => c.PoolId == poolId && c.UserId == userId)
                .Select(c => new ChampionRow(c.Team.Name, c.Team.FlagCode, c.Team.ShortCode, c.Points))
                .FirstOrDefaultAsync();

            championPointsByUser = await db.ChampionPredictions
                .Where(c => c.PoolId == poolId)
                .ToDictionaryAsync(c => c.UserId, c => c.Points);
        }

        // 7) El mejor del día: misma consulta de predicciones del día agregada por usuario
        //    (+ puntos de campeón el día de la Final)
        var totalsByUser = (await db.Predictions
            .Where(p => p.PoolId == poolId && matchIds.Contains(p.MatchId))
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Points = g.Sum(p => p.Points) })
            .ToListAsync())
            .ToDictionary(t => t.UserId, t => t.Points);

        foreach (var (champUserId, champPts) in championPointsByUser)
            totalsByUser[champUserId] = totalsByUser.GetValueOrDefault(champUserId) + champPts;

        var leaders = new List<DayLeader>();
        var maxPoints = totalsByUser.Count > 0 ? totalsByUser.Values.Max() : 0;
        if (maxPoints > 0)
        {
            var leaderIds = totalsByUser
                .Where(t => t.Value == maxPoints)
                .Select(t => t.Key)
                .ToList();

            leaders = (await db.PoolMembers
                .Where(pm => pm.PoolId == poolId && leaderIds.Contains(pm.UserId))
                .Select(pm => new { pm.UserId, pm.User.DisplayName, pm.User.ProfilePicturePath })
                .ToListAsync())
                .Select(x => new DayLeader(x.UserId, x.DisplayName, x.ProfilePicturePath, maxPoints))
                .OrderBy(l => l.DisplayName)
                .ToList();
        }

        var dayPoints = rows.Sum(r => r.Points) + (champion?.Points ?? 0);

        return new DailySummary(
            day, rows, dayPoints, position, previousPosition,
            totalMembers, leaders, availableDays, champion);
    }

    /// <summary>
    /// Posición del usuario según el último snapshot del pool anterior a <paramref name="beforeUtc"/>.
    /// Reutiliza los StandingsSnapshots del Módulo F (uno por partido finalizado, ya en cascada).
    /// </summary>
    private static async Task<int?> SnapshotPositionAsync(
        QuinielaDbContext db, int poolId, int userId, DateTime beforeUtc)
    {
        return await db.StandingsSnapshots
            .Where(s => s.PoolId == poolId && s.UserId == userId && s.Match.KickoffUtc < beforeUtc)
            .OrderByDescending(s => s.Match.KickoffUtc)
            .Select(s => (int?)s.Position)
            .FirstOrDefaultAsync();
    }

    private static (DateTime StartUtc, DateTime EndUtc) DayRangeUtc(DateOnly day)
    {
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(
            day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Tz);
        return (startUtc, startUtc.AddDays(1));
    }
}
