using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quiniela.Data.Entities;

namespace Quiniela.Data.Seeding;

public static class DbInitializer
{
    private record TeamSeed(string Name, string FlagCode, char Group);
    private record MatchSeed(string Home, string Away, DateTime KickoffUtc, char Group);

    // 48 selecciones clasificadas — FIFA World Cup 2026
    private static readonly TeamSeed[] Teams =
    [
        // Grupo A (Ciudad de México / Guadalajara)
        new("México",           "mx",     'A'),
        new("Sudáfrica",        "za",     'A'),
        new("Corea del Sur",    "kr",     'A'),
        new("República Checa",  "cz",     'A'),

        // Grupo B (Los Ángeles / San Francisco / Seattle)
        new("Canadá",                   "ca", 'B'),
        new("Bosnia y Herzegovina",     "ba", 'B'),
        new("Catar",                    "qa", 'B'),
        new("Suiza",                    "ch", 'B'),

        // Grupo C (Boston / Nueva York / Nueva Jersey)
        new("Brasil",     "br",     'C'),
        new("Marruecos",  "ma",     'C'),
        new("Haití",      "ht",     'C'),
        new("Escocia",    "gb-sct", 'C'),

        // Grupo D (Dallas / Houston)
        new("Estados Unidos",  "us", 'D'),
        new("Paraguay",        "py", 'D'),
        new("Australia",       "au", 'D'),
        new("Turquía",         "tr", 'D'),

        // Grupo E (Atlanta / Miami)
        new("Alemania",        "de", 'E'),
        new("Curazao",         "cw", 'E'),
        new("Costa de Marfil", "ci", 'E'),
        new("Ecuador",         "ec", 'E'),

        // Grupo F (Kansas City / Chicago)
        new("Países Bajos",  "nl", 'F'),
        new("Japón",         "jp", 'F'),
        new("Suecia",        "se", 'F'),
        new("Túnez",         "tn", 'F'),

        // Grupo G (Los Ángeles / San Francisco)
        new("Bélgica",       "be", 'G'),
        new("Egipto",        "eg", 'G'),
        new("Irán",          "ir", 'G'),
        new("Nueva Zelanda", "nz", 'G'),

        // Grupo H (Ciudad de México / Monterrey)
        new("España",          "es", 'H'),
        new("Cabo Verde",      "cv", 'H'),
        new("Arabia Saudita",  "sa", 'H'),
        new("Uruguay",         "uy", 'H'),

        // Grupo I (Boston / Nueva York / Nueva Jersey)
        new("Francia",  "fr", 'I'),
        new("Senegal",  "sn", 'I'),
        new("Irak",     "iq", 'I'),
        new("Noruega",  "no", 'I'),

        // Grupo J (Dallas / Kansas City)
        new("Argentina",  "ar", 'J'),
        new("Argelia",    "dz", 'J'),
        new("Austria",    "at", 'J'),
        new("Jordania",   "jo", 'J'),

        // Grupo K (Vancouver / Toronto)
        new("Portugal",    "pt", 'K'),
        new("Congo RD",    "cd", 'K'),
        new("Uzbekistán",  "uz", 'K'),
        new("Colombia",    "co", 'K'),

        // Grupo L (Atlanta / Miami / Filadelfia)
        new("Inglaterra",  "gb-eng", 'L'),
        new("Croacia",     "hr",     'L'),
        new("Ghana",       "gh",     'L'),
        new("Panamá",      "pa",     'L'),
    ];

    // 72 partidos de fase de grupos — horarios en UTC (fuente: ESPN schedule, ET+4h)
    private static readonly MatchSeed[] GroupMatches =
    [
        // ── Grupo A ─────────────────────────────────────────────────
        new("México",          "Sudáfrica",       new(2026, 6, 11, 19,  0, 0, DateTimeKind.Utc), 'A'),
        new("Corea del Sur",   "República Checa", new(2026, 6, 12,  2,  0, 0, DateTimeKind.Utc), 'A'),
        new("República Checa", "Sudáfrica",       new(2026, 6, 18, 16,  0, 0, DateTimeKind.Utc), 'A'),
        new("México",          "Corea del Sur",   new(2026, 6, 19,  3,  0, 0, DateTimeKind.Utc), 'A'),
        new("República Checa", "México",          new(2026, 6, 25,  1,  0, 0, DateTimeKind.Utc), 'A'),
        new("Sudáfrica",       "Corea del Sur",   new(2026, 6, 25,  1,  0, 0, DateTimeKind.Utc), 'A'),

        // ── Grupo B ─────────────────────────────────────────────────
        new("Canadá",                 "Bosnia y Herzegovina", new(2026, 6, 12, 19,  0, 0, DateTimeKind.Utc), 'B'),
        new("Catar",                  "Suiza",                new(2026, 6, 13, 19,  0, 0, DateTimeKind.Utc), 'B'),
        new("Suiza",                  "Bosnia y Herzegovina", new(2026, 6, 18, 19,  0, 0, DateTimeKind.Utc), 'B'),
        new("Canadá",                 "Catar",                new(2026, 6, 18, 22,  0, 0, DateTimeKind.Utc), 'B'),
        new("Suiza",                  "Canadá",               new(2026, 6, 24, 19,  0, 0, DateTimeKind.Utc), 'B'),
        new("Bosnia y Herzegovina",   "Catar",                new(2026, 6, 24, 19,  0, 0, DateTimeKind.Utc), 'B'),

        // ── Grupo C ─────────────────────────────────────────────────
        new("Brasil",    "Marruecos", new(2026, 6, 13, 22,  0, 0, DateTimeKind.Utc), 'C'),
        new("Haití",     "Escocia",   new(2026, 6, 14,  1,  0, 0, DateTimeKind.Utc), 'C'),
        new("Escocia",   "Marruecos", new(2026, 6, 19, 22,  0, 0, DateTimeKind.Utc), 'C'),
        new("Brasil",    "Haití",     new(2026, 6, 20,  1,  0, 0, DateTimeKind.Utc), 'C'),
        new("Escocia",   "Brasil",    new(2026, 6, 24, 22,  0, 0, DateTimeKind.Utc), 'C'),
        new("Marruecos", "Haití",     new(2026, 6, 24, 22,  0, 0, DateTimeKind.Utc), 'C'),

        // ── Grupo D ─────────────────────────────────────────────────
        new("Estados Unidos", "Paraguay",       new(2026, 6, 13,  1,  0, 0, DateTimeKind.Utc), 'D'),
        new("Australia",      "Turquía",        new(2026, 6, 14,  4,  0, 0, DateTimeKind.Utc), 'D'),
        new("Estados Unidos", "Australia",      new(2026, 6, 19, 19,  0, 0, DateTimeKind.Utc), 'D'),
        new("Turquía",        "Paraguay",       new(2026, 6, 20,  4,  0, 0, DateTimeKind.Utc), 'D'),
        new("Turquía",        "Estados Unidos", new(2026, 6, 26,  2,  0, 0, DateTimeKind.Utc), 'D'),
        new("Paraguay",       "Australia",      new(2026, 6, 26,  2,  0, 0, DateTimeKind.Utc), 'D'),

        // ── Grupo E ─────────────────────────────────────────────────
        new("Alemania",        "Curazao",        new(2026, 6, 14, 17,  0, 0, DateTimeKind.Utc), 'E'),
        new("Costa de Marfil", "Ecuador",        new(2026, 6, 14, 23,  0, 0, DateTimeKind.Utc), 'E'),
        new("Alemania",        "Costa de Marfil", new(2026, 6, 20, 20,  0, 0, DateTimeKind.Utc), 'E'),
        new("Ecuador",         "Curazao",        new(2026, 6, 21,  0,  0, 0, DateTimeKind.Utc), 'E'),
        new("Ecuador",         "Alemania",       new(2026, 6, 25, 20,  0, 0, DateTimeKind.Utc), 'E'),
        new("Curazao",         "Costa de Marfil", new(2026, 6, 25, 20,  0, 0, DateTimeKind.Utc), 'E'),

        // ── Grupo F ─────────────────────────────────────────────────
        new("Países Bajos", "Japón",        new(2026, 6, 14, 20,  0, 0, DateTimeKind.Utc), 'F'),
        new("Suecia",       "Túnez",        new(2026, 6, 15,  2,  0, 0, DateTimeKind.Utc), 'F'),
        new("Países Bajos", "Suecia",       new(2026, 6, 20, 17,  0, 0, DateTimeKind.Utc), 'F'),
        new("Túnez",        "Japón",        new(2026, 6, 21,  4,  0, 0, DateTimeKind.Utc), 'F'),
        new("Japón",        "Suecia",       new(2026, 6, 25, 23,  0, 0, DateTimeKind.Utc), 'F'),
        new("Túnez",        "Países Bajos", new(2026, 6, 25, 23,  0, 0, DateTimeKind.Utc), 'F'),

        // ── Grupo G ─────────────────────────────────────────────────
        new("Bélgica",      "Egipto",       new(2026, 6, 15, 22,  0, 0, DateTimeKind.Utc), 'G'),
        new("Irán",         "Nueva Zelanda", new(2026, 6, 16,  4,  0, 0, DateTimeKind.Utc), 'G'),
        new("Bélgica",      "Irán",         new(2026, 6, 21, 19,  0, 0, DateTimeKind.Utc), 'G'),
        new("Nueva Zelanda", "Egipto",      new(2026, 6, 22,  1,  0, 0, DateTimeKind.Utc), 'G'),
        new("Egipto",       "Irán",         new(2026, 6, 27,  3,  0, 0, DateTimeKind.Utc), 'G'),
        new("Nueva Zelanda", "Bélgica",     new(2026, 6, 27,  3,  0, 0, DateTimeKind.Utc), 'G'),

        // ── Grupo H ─────────────────────────────────────────────────
        new("España",         "Cabo Verde",    new(2026, 6, 15, 17,  0, 0, DateTimeKind.Utc), 'H'),
        new("Arabia Saudita", "Uruguay",       new(2026, 6, 15, 22,  0, 0, DateTimeKind.Utc), 'H'),
        new("España",         "Arabia Saudita", new(2026, 6, 21, 16,  0, 0, DateTimeKind.Utc), 'H'),
        new("Uruguay",        "Cabo Verde",    new(2026, 6, 21, 22,  0, 0, DateTimeKind.Utc), 'H'),
        new("Cabo Verde",     "Arabia Saudita", new(2026, 6, 27,  0,  0, 0, DateTimeKind.Utc), 'H'),
        new("Uruguay",        "España",        new(2026, 6, 27,  0,  0, 0, DateTimeKind.Utc), 'H'),

        // ── Grupo I ─────────────────────────────────────────────────
        new("Francia", "Senegal", new(2026, 6, 16, 19,  0, 0, DateTimeKind.Utc), 'I'),
        new("Irak",    "Noruega", new(2026, 6, 16, 22,  0, 0, DateTimeKind.Utc), 'I'),
        new("Francia", "Irak",    new(2026, 6, 22, 21,  0, 0, DateTimeKind.Utc), 'I'),
        new("Noruega", "Senegal", new(2026, 6, 23,  0,  0, 0, DateTimeKind.Utc), 'I'),
        new("Noruega", "Francia", new(2026, 6, 26, 19,  0, 0, DateTimeKind.Utc), 'I'),
        new("Senegal", "Irak",    new(2026, 6, 26, 19,  0, 0, DateTimeKind.Utc), 'I'),

        // ── Grupo J ─────────────────────────────────────────────────
        new("Argentina", "Argelia",   new(2026, 6, 17,  1,  0, 0, DateTimeKind.Utc), 'J'),
        new("Austria",   "Jordania",  new(2026, 6, 17,  4,  0, 0, DateTimeKind.Utc), 'J'),
        new("Argentina", "Austria",   new(2026, 6, 22, 17,  0, 0, DateTimeKind.Utc), 'J'),
        new("Jordania",  "Argelia",   new(2026, 6, 23,  3,  0, 0, DateTimeKind.Utc), 'J'),
        new("Argelia",   "Austria",   new(2026, 6, 28,  2,  0, 0, DateTimeKind.Utc), 'J'),
        new("Jordania",  "Argentina", new(2026, 6, 28,  2,  0, 0, DateTimeKind.Utc), 'J'),

        // ── Grupo K ─────────────────────────────────────────────────
        new("Portugal",   "Congo RD",    new(2026, 6, 17, 17,  0, 0, DateTimeKind.Utc), 'K'),
        new("Uzbekistán", "Colombia",    new(2026, 6, 18,  2,  0, 0, DateTimeKind.Utc), 'K'),
        new("Portugal",   "Uzbekistán",  new(2026, 6, 23, 17,  0, 0, DateTimeKind.Utc), 'K'),
        new("Colombia",   "Congo RD",    new(2026, 6, 24,  2,  0, 0, DateTimeKind.Utc), 'K'),
        new("Colombia",   "Portugal",    new(2026, 6, 27, 23, 30, 0, DateTimeKind.Utc), 'K'),
        new("Congo RD",   "Uzbekistán",  new(2026, 6, 27, 23, 30, 0, DateTimeKind.Utc), 'K'),

        // ── Grupo L ─────────────────────────────────────────────────
        new("Inglaterra", "Croacia",    new(2026, 6, 17, 20,  0, 0, DateTimeKind.Utc), 'L'),
        new("Ghana",      "Panamá",     new(2026, 6, 17, 23,  0, 0, DateTimeKind.Utc), 'L'),
        new("Inglaterra", "Ghana",      new(2026, 6, 23, 20,  0, 0, DateTimeKind.Utc), 'L'),
        new("Panamá",     "Croacia",    new(2026, 6, 23, 23,  0, 0, DateTimeKind.Utc), 'L'),
        new("Panamá",     "Inglaterra", new(2026, 6, 27, 21,  0, 0, DateTimeKind.Utc), 'L'),
        new("Croacia",    "Ghana",      new(2026, 6, 27, 21,  0, 0, DateTimeKind.Utc), 'L'),
    ];

    public static async Task SeedAsync(
        QuinielaDbContext context,
        UserManager<User> userManager,
        RoleManager<IdentityRole<int>> roleManager,
        IConfiguration configuration,
        ILogger logger)
    {
        await SeedTeamsAndMatchesAsync(context, logger);
        await SeedUsersAsync(userManager, roleManager, configuration, logger);
    }

    private static async Task SeedTeamsAndMatchesAsync(QuinielaDbContext context, ILogger logger)
    {
        if (await context.Teams.AnyAsync())
        {
            logger.LogInformation("Teams already seeded — skipping.");
            return;
        }

        var teams = Teams
            .Select(t => new Team { Name = t.Name, FlagCode = t.FlagCode, GroupCode = t.Group })
            .ToList();

        context.Teams.AddRange(teams);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} teams.", teams.Count);

        var byName = teams.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        var matches = GroupMatches
            .Select(m => new Match
            {
                HomeTeam   = byName[m.Home],
                AwayTeam   = byName[m.Away],
                KickoffUtc = m.KickoffUtc,
                Stage      = MatchStage.Grupos,
                GroupCode  = m.Group,
                Status     = MatchStatus.Programado,
            })
            .ToList();

        context.Matches.AddRange(matches);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} group-stage matches.", matches.Count);
    }

    private static async Task SeedUsersAsync(
        UserManager<User> userManager,
        RoleManager<IdentityRole<int>> roleManager,
        IConfiguration configuration,
        ILogger logger)
    {
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole<int>("Admin"));
            logger.LogInformation("Created 'Admin' role.");
        }

        if (await userManager.Users.AnyAsync())
        {
            // Users exist; make sure the admin user actually has the Admin role.
            var existingAdmin = await userManager.FindByNameAsync("admin");
            if (existingAdmin is not null && !await userManager.IsInRoleAsync(existingAdmin, "Admin"))
            {
                await userManager.AddToRoleAsync(existingAdmin, "Admin");
                logger.LogInformation("Assigned 'Admin' role to existing admin user.");
            }
            logger.LogInformation("Users already seeded — skipping creation.");
            return;
        }

        var adminPassword = configuration["Seed:AdminPassword"]
            ?? throw new InvalidOperationException(
                "Seed:AdminPassword is not set. " +
                "Run: dotnet user-secrets set \"Seed:AdminPassword\" \"<contraseña>\" --project src/Quiniela.Web");

        var playerPassword = configuration["Seed:PlayerPassword"]
            ?? throw new InvalidOperationException(
                "Seed:PlayerPassword is not set. " +
                "Run: dotnet user-secrets set \"Seed:PlayerPassword\" \"<contraseña>\" --project src/Quiniela.Web");

        await CreateUserAsync(userManager, logger,
            userName: "admin", displayName: "Administrador",
            isAdmin: true, password: adminPassword, role: "Admin");

        await CreateUserAsync(userManager, logger,
            userName: "jugador1", displayName: "Jugador 1",
            isAdmin: false, password: playerPassword);

        await CreateUserAsync(userManager, logger,
            userName: "jugador2", displayName: "Jugador 2",
            isAdmin: false, password: playerPassword);
    }

    private static async Task CreateUserAsync(
        UserManager<User> userManager,
        ILogger logger,
        string userName, string displayName,
        bool isAdmin, string password,
        string? role = null)
    {
        var user = new User
        {
            UserName    = userName,
            DisplayName = displayName,
            IsAdmin     = isAdmin,
            CreatedAt   = DateTime.UtcNow,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Failed to create '{userName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");

        if (role is not null)
            await userManager.AddToRoleAsync(user, role);

        logger.LogInformation("Created user '{UserName}' (admin={IsAdmin}).", userName, isAdmin);
    }
}
