using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class PageVisitService(IDbContextFactory<QuinielaDbContext> dbFactory)
{
    public record VisitRow(string DisplayName, string? ProfilePicturePath,
                           string PageName, DateTime VisitedAtUtc);

    public record VisitPage(List<VisitRow> Rows, int TotalCount);

    /// <summary>
    /// Registra una visita. NUNCA debe tumbar la página que la llama:
    /// try/catch total, silent-fail (mismo principio que PushNotificationService.SendAsync).
    /// </summary>
    public async Task LogAsync(int userId, int poolId, string pageName, string url)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            db.PageVisitLogs.Add(new PageVisitLog
            {
                UserId = userId,
                PoolId = poolId,
                PageName = pageName,
                Url = url.Length > 300 ? url[..300] : url, // query strings largos de Comparar
                VisitedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        catch
        {
            // El log de visitas es accesorio: cualquier fallo (BD caída, etc.) se ignora.
        }
    }

    /// <summary>
    /// Página de resultados: filtro opcional por usuario, orden VisitedAt desc,
    /// Skip/Take en servidor (nunca se materializa la tabla completa).
    /// </summary>
    public async Task<VisitPage> GetPageAsync(int poolId, int? userId, int page, int pageSize,
                                              string? pageName = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var query = db.PageVisitLogs.Where(v => v.PoolId == poolId);
        if (userId is not null)
            query = query.Where(v => v.UserId == userId);
        if (pageName is not null)
            query = query.Where(v => v.PageName == pageName);

        var totalCount = await query.CountAsync();

        var rows = await query
            .OrderByDescending(v => v.VisitedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VisitRow(
                v.User.DisplayName,
                v.User.ProfilePicturePath,
                v.PageName,
                v.VisitedAt))
            .ToListAsync();

        return new VisitPage(rows, totalCount);
    }

    /// <summary>
    /// Módulos (PageName) distintos con visitas en la sala, para poblar el filtro
    /// solo con páginas que sí tienen registros.
    /// </summary>
    public async Task<List<string>> GetDistinctPageNamesAsync(int poolId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.PageVisitLogs
            .Where(v => v.PoolId == poolId)
            .Select(v => v.PageName)
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync();
    }
}
