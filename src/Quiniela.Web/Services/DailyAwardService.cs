using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

/// <summary>
/// Mejor/peor del día por sala, calculado siempre on-demand desde Predictions +
/// Matches finalizados agrupados por fecha local CDMX (sin tabla nueva, se
/// auto-corrige si el admin corrige un marcador viejo). Todos los miembros de
/// la sala participan: no pronosticar un día cuenta como 0 puntos. Si max == min
/// (todos empatados, incluye "nadie pronosticó") el día no otorga nada.
/// Consumido por AchievementsService (conteo de medallas) y N10 (día específico).
/// </summary>
public class DailyAwardService(IDbContextFactory<QuinielaDbContext> dbFactory)
{
    // Misma zona fija que DailySummaryService/NotificationCheckService: sin DST desde 2022.
    private static readonly TimeZoneInfo Tz =
        TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    public record DayAwards(
        DateOnly Day,
        List<int> BestUserIds,    // todos los empatados con el máximo
        List<int> WorstUserIds,   // todos los empatados con el mínimo
        int MaxPoints,
        int MinPoints);

    /// <summary>
    /// Todos los días con >= 1 partido finalizado que otorgaron premio (max != min),
    /// incluido el día en curso. Base para el conteo de medallas.
    /// </summary>
    public async Task<List<DayAwards>> GetAllAsync(int poolId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var finalized = await db.Matches
            .Where(m => m.Status == MatchStatus.Finalizado)
            .Select(m => new { m.Id, m.KickoffUtc, m.Stage })
            .ToListAsync();

        if (finalized.Count == 0) return [];

        var memberIds = await db.PoolMembers
            .Where(pm => pm.PoolId == poolId)
            .Select(pm => pm.UserId)
            .ToListAsync();

        if (memberIds.Count == 0) return [];

        var dayByMatch = finalized.ToDictionary(
            m => m.Id,
            m => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(m.KickoffUtc, Tz)));

        var matchIds = finalized.Select(m => m.Id).ToList();
        var predictions = await db.Predictions
            .Where(p => p.PoolId == poolId && matchIds.Contains(p.MatchId))
            .Select(p => new { p.UserId, p.MatchId, p.Points })
            .ToListAsync();

        var totals = new Dictionary<(DateOnly Day, int UserId), int>();
        foreach (var p in predictions)
        {
            var key = (dayByMatch[p.MatchId], p.UserId);
            totals[key] = totals.GetValueOrDefault(key) + p.Points;
        }

        // El día de la Final se suman los puntos de campeón — mismo criterio
        // que los DayLeaders de DailySummaryService (Módulo L).
        var finalMatch = finalized.FirstOrDefault(m => m.Stage == MatchStage.Final);
        if (finalMatch is not null)
        {
            var finalDay = dayByMatch[finalMatch.Id];
            var championPoints = await db.ChampionPredictions
                .Where(c => c.PoolId == poolId)
                .Select(c => new { c.UserId, c.Points })
                .ToListAsync();

            foreach (var c in championPoints)
                totals[(finalDay, c.UserId)] = totals.GetValueOrDefault((finalDay, c.UserId)) + c.Points;
        }

        var result = new List<DayAwards>();
        foreach (var day in dayByMatch.Values.Distinct().OrderBy(d => d))
        {
            // Total del día por miembro: 0 si no tiene predicciones (sí participa)
            var perMember = memberIds.ToDictionary(id => id, id => totals.GetValueOrDefault((day, id)));

            var max = perMember.Values.Max();
            var min = perMember.Values.Min();
            if (max == min) continue; // todos empatados: no se otorga nada

            result.Add(new DayAwards(
                day,
                perMember.Where(kv => kv.Value == max).Select(kv => kv.Key).ToList(),
                perMember.Where(kv => kv.Value == min).Select(kv => kv.Key).ToList(),
                max, min));
        }

        return result;
    }

    /// <summary>Conteo de medallas por usuario: (veces mejor, veces peor).</summary>
    public async Task<Dictionary<int, (int Best, int Worst)>> GetCountsAsync(int poolId)
    {
        var counts = new Dictionary<int, (int Best, int Worst)>();
        foreach (var day in await GetAllAsync(poolId))
        {
            foreach (var userId in day.BestUserIds)
            {
                var c = counts.GetValueOrDefault(userId);
                counts[userId] = (c.Best + 1, c.Worst);
            }
            foreach (var userId in day.WorstUserIds)
            {
                var c = counts.GetValueOrDefault(userId);
                counts[userId] = (c.Best, c.Worst + 1);
            }
        }
        return counts;
    }

    /// <summary>Un día específico (lo usa N10 a las 21:30). Null si el día no otorga premio.</summary>
    public async Task<DayAwards?> GetForDayAsync(int poolId, DateOnly day)
    {
        var all = await GetAllAsync(poolId);
        return all.FirstOrDefault(a => a.Day == day);
    }
}
