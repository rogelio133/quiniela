using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Quiniela.Data;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Services;

public class ProfileService(IDbContextFactory<QuinielaDbContext> dbFactory, IWebHostEnvironment env)
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private const long MaxFileSize = 2 * 1024 * 1024; // 2 MB

    public async Task<User?> GetUserAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.FindAsync(userId);
    }

    public async Task<(bool Success, string? Error)> UpdateDisplayNameAsync(int userId, string displayName)
    {
        var name = displayName.Trim();
        if (string.IsNullOrEmpty(name)) return (false, "El nombre no puede estar vacío.");
        if (name.Length > 50) return (false, "El nombre no puede tener más de 50 caracteres.");

        await using var db = await dbFactory.CreateDbContextAsync();

        var user = await db.Users.FindAsync(userId);
        if (user is null) return (false, "Usuario no encontrado.");

        user.DisplayName = name;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error, string? Path)> SaveProfilePictureAsync(int userId, IBrowserFile file)
    {
        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return (false, "Formato no válido. Usa JPG, PNG, WebP o GIF.", null);

        var dir = Path.Combine(env.WebRootPath, "uploads", "avatars");
        Directory.CreateDirectory(dir);

        var fileName = $"user-{userId}{ext}";
        var filePath = Path.Combine(dir, fileName);

        try
        {
            await using var readStream = file.OpenReadStream(maxAllowedSize: MaxFileSize);
            await using var writeStream = File.Create(filePath);
            await readStream.CopyToAsync(writeStream);
        }
        catch (IOException)
        {
            return (false, "El archivo supera el tamaño máximo de 2 MB.", null);
        }

        var relativePath = $"/uploads/avatars/{fileName}";

        await using var db = await dbFactory.CreateDbContextAsync();

        var user = await db.Users.FindAsync(userId);
        if (user is null) return (false, "Usuario no encontrado.", null);

        user.ProfilePicturePath = relativePath;
        await db.SaveChangesAsync();
        return (true, null, relativePath);
    }
}
