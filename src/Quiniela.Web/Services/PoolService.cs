using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class PoolService(IDbContextFactory<QuinielaDbContext> dbFactory)
{
    private static readonly char[] CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    public async Task<Pool> CreatePoolAsync(string name, int ownerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        string code;
        do { code = GenerateCode(); }
        while (await db.Pools.AnyAsync(p => p.JoinCode == code));

        var pool = new Pool
        {
            Name = name,
            JoinCode = code,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow
        };
        db.Pools.Add(pool);
        db.PoolMembers.Add(new PoolMember { Pool = pool, UserId = ownerId });
        await db.SaveChangesAsync();
        return pool;
    }

    public async Task<(bool Success, string? Error)> JoinPoolAsync(string joinCode, int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var normalized = joinCode.Trim().ToUpperInvariant();
        var pool = await db.Pools.FirstOrDefaultAsync(p => p.JoinCode == normalized);
        if (pool is null)
            return (false, "Código inválido o inexistente.");

        var alreadyMember = await db.PoolMembers
            .AnyAsync(m => m.PoolId == pool.Id && m.UserId == userId);
        if (alreadyMember)
            return (false, "Ya eres miembro de esta sala.");

        db.PoolMembers.Add(new PoolMember { PoolId = pool.Id, UserId = userId });
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<Pool>> GetUserPoolsAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Pools
            .Where(p => p.Members.Any(m => m.UserId == userId))
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Pool?> GetPoolWithMembersAsync(int poolId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Pools
            .Include(p => p.Owner)
            .Include(p => p.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(p => p.Id == poolId);
    }

    public async Task<bool> UpdatePoolNameAsync(int poolId, int requestingUserId, string newName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var pool = await db.Pools.FindAsync(poolId);
        if (pool is null || pool.OwnerId != requestingUserId)
            return false;
        pool.Name = newName;
        await db.SaveChangesAsync();
        return true;
    }

    private static string GenerateCode() =>
        new(Enumerable.Range(0, 6)
            .Select(_ => CodeChars[Random.Shared.Next(CodeChars.Length)])
            .ToArray());
}
