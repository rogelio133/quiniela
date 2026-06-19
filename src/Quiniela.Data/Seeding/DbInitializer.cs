using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quiniela.Data.Entities;

namespace Quiniela.Data.Seeding;

public static class DbInitializer
{
    private record TeamSeed(string Name, string FlagCode, string ShortCode, char Group);
    private record MatchSeed(string Home, string Away, DateTime KickoffUtc, char Group, string Venue);

    // 48 selecciones clasificadas — FIFA World Cup 2026
    private static readonly TeamSeed[] Teams =
    [
        // Grupo A (Ciudad de México / Guadalajara)
        new("México",           "mx",     "MEX", 'A'),
        new("Sudáfrica",        "za",     "RSA", 'A'),
        new("Corea del Sur",    "kr",     "KOR", 'A'),
        new("República Checa",  "cz",     "CZE", 'A'),

        // Grupo B (Toronto / Vancouver / Seattle)
        new("Canadá",                   "ca", "CAN", 'B'),
        new("Bosnia y Herzegovina",     "ba", "BIH", 'B'),
        new("Catar",                    "qa", "QAT", 'B'),
        new("Suiza",                    "ch", "SUI", 'B'),

        // Grupo C (Boston / Nueva York / Nueva Jersey)
        new("Brasil",     "br",     "BRA", 'C'),
        new("Marruecos",  "ma",     "MAR", 'C'),
        new("Haití",      "ht",     "HAI", 'C'),
        new("Escocia",    "gb-sct", "SCO", 'C'),

        // Grupo D (Dallas / Houston)
        new("Estados Unidos",  "us", "USA", 'D'),
        new("Paraguay",        "py", "PAR", 'D'),
        new("Australia",       "au", "AUS", 'D'),
        new("Turquía",         "tr", "TUR", 'D'),

        // Grupo E (Atlanta / Miami)
        new("Alemania",        "de", "GER", 'E'),
        new("Curazao",         "cw", "CUW", 'E'),
        new("Costa de Marfil", "ci", "CIV", 'E'),
        new("Ecuador",         "ec", "ECU", 'E'),

        // Grupo F (Kansas City / Seattle)
        new("Países Bajos",  "nl", "NED", 'F'),
        new("Japón",         "jp", "JPN", 'F'),
        new("Suecia",        "se", "SWE", 'F'),
        new("Túnez",         "tn", "TUN", 'F'),

        // Grupo G (Los Ángeles / San Francisco)
        new("Bélgica",       "be", "BEL", 'G'),
        new("Egipto",        "eg", "EGY", 'G'),
        new("Irán",          "ir", "IRN", 'G'),
        new("Nueva Zelanda", "nz", "NZL", 'G'),

        // Grupo H (Ciudad de México / Monterrey)
        new("España",          "es", "ESP", 'H'),
        new("Cabo Verde",      "cv", "CPV", 'H'),
        new("Arabia Saudita",  "sa", "KSA", 'H'),
        new("Uruguay",         "uy", "URU", 'H'),

        // Grupo I (Nueva York / Boston / Filadelfia)
        new("Francia",  "fr", "FRA", 'I'),
        new("Senegal",  "sn", "SEN", 'I'),
        new("Irak",     "iq", "IRQ", 'I'),
        new("Noruega",  "no", "NOR", 'I'),

        // Grupo J (Dallas / Kansas City)
        new("Argentina",  "ar", "ARG", 'J'),
        new("Argelia",    "dz", "ALG", 'J'),
        new("Austria",    "at", "AUT", 'J'),
        new("Jordania",   "jo", "JOR", 'J'),

        // Grupo K (Vancouver / Toronto)
        new("Portugal",    "pt", "POR", 'K'),
        new("Congo RD",    "cd", "COD", 'K'),
        new("Uzbekistán",  "uz", "UZB", 'K'),
        new("Colombia",    "co", "COL", 'K'),

        // Grupo L (Atlanta / Miami / Filadelfia)
        new("Inglaterra",  "gb-eng", "ENG", 'L'),
        new("Croacia",     "hr",     "CRO", 'L'),
        new("Ghana",       "gh",     "GHA", 'L'),
        new("Panamá",      "pa",     "PAN", 'L'),
    ];

    // 72 partidos de fase de grupos — horarios en UTC (fuente: ESPN schedule, ET+4h)
    private static readonly MatchSeed[] GroupMatches =
    [
        // ── Grupo A ─────────────────────────────────────────────────
        new("México",          "Sudáfrica",       new(2026, 6, 11, 19,  0, 0, DateTimeKind.Utc), 'A', "Estadio Azteca"),
        new("Corea del Sur",   "República Checa", new(2026, 6, 12,  2,  0, 0, DateTimeKind.Utc), 'A', "Estadio Guadalajara"),
        new("República Checa", "Sudáfrica",       new(2026, 6, 18, 16,  0, 0, DateTimeKind.Utc), 'A', "Estadio Guadalajara"),
        new("México",          "Corea del Sur",   new(2026, 6, 19,  3,  0, 0, DateTimeKind.Utc), 'A', "Estadio Azteca"),
        new("República Checa", "México",          new(2026, 6, 25,  1,  0, 0, DateTimeKind.Utc), 'A', "Estadio Guadalajara"),
        new("Sudáfrica",       "Corea del Sur",   new(2026, 6, 25,  1,  0, 0, DateTimeKind.Utc), 'A', "Estadio Azteca"),

        // ── Grupo B ─────────────────────────────────────────────────
        new("Canadá",                 "Bosnia y Herzegovina", new(2026, 6, 12, 19,  0, 0, DateTimeKind.Utc), 'B', "BMO Field"),
        new("Catar",                  "Suiza",                new(2026, 6, 13, 19,  0, 0, DateTimeKind.Utc), 'B', "Lumen Field"),
        new("Suiza",                  "Bosnia y Herzegovina", new(2026, 6, 18, 19,  0, 0, DateTimeKind.Utc), 'B', "BC Place"),
        new("Canadá",                 "Catar",                new(2026, 6, 18, 22,  0, 0, DateTimeKind.Utc), 'B', "BMO Field"),
        new("Suiza",                  "Canadá",               new(2026, 6, 24, 19,  0, 0, DateTimeKind.Utc), 'B', "Lumen Field"),
        new("Bosnia y Herzegovina",   "Catar",                new(2026, 6, 24, 19,  0, 0, DateTimeKind.Utc), 'B', "BC Place"),

        // ── Grupo C ─────────────────────────────────────────────────
        new("Brasil",    "Marruecos", new(2026, 6, 13, 22,  0, 0, DateTimeKind.Utc), 'C', "MetLife Stadium"),
        new("Haití",     "Escocia",   new(2026, 6, 14,  1,  0, 0, DateTimeKind.Utc), 'C', "Gillette Stadium"),
        new("Escocia",   "Marruecos", new(2026, 6, 19, 22,  0, 0, DateTimeKind.Utc), 'C', "MetLife Stadium"),
        new("Brasil",    "Haití",     new(2026, 6, 20,  1,  0, 0, DateTimeKind.Utc), 'C', "Gillette Stadium"),
        new("Escocia",   "Brasil",    new(2026, 6, 24, 22,  0, 0, DateTimeKind.Utc), 'C', "MetLife Stadium"),
        new("Marruecos", "Haití",     new(2026, 6, 24, 22,  0, 0, DateTimeKind.Utc), 'C', "Gillette Stadium"),

        // ── Grupo D ─────────────────────────────────────────────────
        new("Estados Unidos", "Paraguay",       new(2026, 6, 13,  1,  0, 0, DateTimeKind.Utc), 'D', "AT&T Stadium"),
        new("Australia",      "Turquía",        new(2026, 6, 14,  4,  0, 0, DateTimeKind.Utc), 'D', "NRG Stadium"),
        new("Estados Unidos", "Australia",      new(2026, 6, 19, 19,  0, 0, DateTimeKind.Utc), 'D', "AT&T Stadium"),
        new("Turquía",        "Paraguay",       new(2026, 6, 20,  3,  0, 0, DateTimeKind.Utc), 'D', "NRG Stadium"),
        new("Turquía",        "Estados Unidos", new(2026, 6, 26,  2,  0, 0, DateTimeKind.Utc), 'D', "NRG Stadium"),
        new("Paraguay",       "Australia",      new(2026, 6, 26,  2,  0, 0, DateTimeKind.Utc), 'D', "AT&T Stadium"),

        // ── Grupo E ─────────────────────────────────────────────────
        new("Alemania",        "Curazao",         new(2026, 6, 14, 17,  0, 0, DateTimeKind.Utc), 'E', "Mercedes-Benz Stadium"),
        new("Costa de Marfil", "Ecuador",         new(2026, 6, 14, 23,  0, 0, DateTimeKind.Utc), 'E', "Hard Rock Stadium"),
        new("Alemania",        "Costa de Marfil", new(2026, 6, 20, 20,  0, 0, DateTimeKind.Utc), 'E', "Mercedes-Benz Stadium"),
        new("Ecuador",         "Curazao",         new(2026, 6, 21,  0,  0, 0, DateTimeKind.Utc), 'E', "Hard Rock Stadium"),
        new("Ecuador",         "Alemania",        new(2026, 6, 25, 20,  0, 0, DateTimeKind.Utc), 'E', "Hard Rock Stadium"),
        new("Curazao",         "Costa de Marfil", new(2026, 6, 25, 20,  0, 0, DateTimeKind.Utc), 'E', "Mercedes-Benz Stadium"),

        // ── Grupo F ─────────────────────────────────────────────────
        new("Países Bajos", "Japón",        new(2026, 6, 14, 20,  0, 0, DateTimeKind.Utc), 'F', "Arrowhead Stadium"),
        new("Suecia",       "Túnez",        new(2026, 6, 15,  2,  0, 0, DateTimeKind.Utc), 'F', "Lumen Field"),
        new("Países Bajos", "Suecia",       new(2026, 6, 20, 17,  0, 0, DateTimeKind.Utc), 'F', "Arrowhead Stadium"),
        new("Túnez",        "Japón",        new(2026, 6, 21,  4,  0, 0, DateTimeKind.Utc), 'F', "Lumen Field"),
        new("Japón",        "Suecia",       new(2026, 6, 25, 23,  0, 0, DateTimeKind.Utc), 'F', "Lumen Field"),
        new("Túnez",        "Países Bajos", new(2026, 6, 25, 23,  0, 0, DateTimeKind.Utc), 'F', "Arrowhead Stadium"),

        // ── Grupo G ─────────────────────────────────────────────────
        new("Bélgica",       "Egipto",        new(2026, 6, 15, 22,  0, 0, DateTimeKind.Utc), 'G', "SoFi Stadium"),
        new("Irán",          "Nueva Zelanda", new(2026, 6, 16,  4,  0, 0, DateTimeKind.Utc), 'G', "Levi's Stadium"),
        new("Bélgica",       "Irán",          new(2026, 6, 21, 19,  0, 0, DateTimeKind.Utc), 'G', "SoFi Stadium"),
        new("Nueva Zelanda", "Egipto",        new(2026, 6, 22,  1,  0, 0, DateTimeKind.Utc), 'G', "Levi's Stadium"),
        new("Egipto",        "Irán",          new(2026, 6, 27,  3,  0, 0, DateTimeKind.Utc), 'G', "Levi's Stadium"),
        new("Nueva Zelanda", "Bélgica",       new(2026, 6, 27,  3,  0, 0, DateTimeKind.Utc), 'G', "SoFi Stadium"),

        // ── Grupo H ─────────────────────────────────────────────────
        new("España",         "Cabo Verde",     new(2026, 6, 15, 17,  0, 0, DateTimeKind.Utc), 'H', "Estadio Azteca"),
        new("Arabia Saudita", "Uruguay",        new(2026, 6, 15, 22,  0, 0, DateTimeKind.Utc), 'H', "Estadio BBVA"),
        new("España",         "Arabia Saudita", new(2026, 6, 21, 16,  0, 0, DateTimeKind.Utc), 'H', "Estadio Azteca"),
        new("Uruguay",        "Cabo Verde",     new(2026, 6, 21, 22,  0, 0, DateTimeKind.Utc), 'H', "Estadio BBVA"),
        new("Cabo Verde",     "Arabia Saudita", new(2026, 6, 27,  0,  0, 0, DateTimeKind.Utc), 'H', "Estadio BBVA"),
        new("Uruguay",        "España",         new(2026, 6, 27,  0,  0, 0, DateTimeKind.Utc), 'H', "Estadio Azteca"),

        // ── Grupo I ─────────────────────────────────────────────────
        new("Francia", "Senegal", new(2026, 6, 16, 19,  0, 0, DateTimeKind.Utc), 'I', "MetLife Stadium"),
        new("Irak",    "Noruega", new(2026, 6, 16, 22,  0, 0, DateTimeKind.Utc), 'I', "Lincoln Financial Field"),
        new("Francia", "Irak",    new(2026, 6, 22, 21,  0, 0, DateTimeKind.Utc), 'I', "Gillette Stadium"),
        new("Noruega", "Senegal", new(2026, 6, 23,  0,  0, 0, DateTimeKind.Utc), 'I', "Lincoln Financial Field"),
        new("Noruega", "Francia", new(2026, 6, 26, 19,  0, 0, DateTimeKind.Utc), 'I', "MetLife Stadium"),
        new("Senegal", "Irak",    new(2026, 6, 26, 19,  0, 0, DateTimeKind.Utc), 'I', "Gillette Stadium"),

        // ── Grupo J ─────────────────────────────────────────────────
        new("Argentina", "Argelia",   new(2026, 6, 17,  1,  0, 0, DateTimeKind.Utc), 'J', "AT&T Stadium"),
        new("Austria",   "Jordania",  new(2026, 6, 17,  4,  0, 0, DateTimeKind.Utc), 'J', "Arrowhead Stadium"),
        new("Argentina", "Austria",   new(2026, 6, 22, 17,  0, 0, DateTimeKind.Utc), 'J', "AT&T Stadium"),
        new("Jordania",  "Argelia",   new(2026, 6, 23,  3,  0, 0, DateTimeKind.Utc), 'J', "Arrowhead Stadium"),
        new("Argelia",   "Austria",   new(2026, 6, 28,  2,  0, 0, DateTimeKind.Utc), 'J', "Arrowhead Stadium"),
        new("Jordania",  "Argentina", new(2026, 6, 28,  2,  0, 0, DateTimeKind.Utc), 'J', "AT&T Stadium"),

        // ── Grupo K ─────────────────────────────────────────────────
        new("Portugal",   "Congo RD",   new(2026, 6, 17, 17,  0, 0, DateTimeKind.Utc), 'K', "BC Place"),
        new("Uzbekistán", "Colombia",   new(2026, 6, 18,  2,  0, 0, DateTimeKind.Utc), 'K', "BMO Field"),
        new("Portugal",   "Uzbekistán", new(2026, 6, 23, 17,  0, 0, DateTimeKind.Utc), 'K', "BC Place"),
        new("Colombia",   "Congo RD",   new(2026, 6, 24,  2,  0, 0, DateTimeKind.Utc), 'K', "BMO Field"),
        new("Colombia",   "Portugal",   new(2026, 6, 27, 23, 30, 0, DateTimeKind.Utc), 'K', "BMO Field"),
        new("Congo RD",   "Uzbekistán", new(2026, 6, 27, 23, 30, 0, DateTimeKind.Utc), 'K', "BC Place"),

        // ── Grupo L ─────────────────────────────────────────────────
        new("Inglaterra", "Croacia",    new(2026, 6, 17, 20,  0, 0, DateTimeKind.Utc), 'L', "Hard Rock Stadium"),
        new("Ghana",      "Panamá",     new(2026, 6, 17, 23,  0, 0, DateTimeKind.Utc), 'L', "Mercedes-Benz Stadium"),
        new("Inglaterra", "Ghana",      new(2026, 6, 23, 20,  0, 0, DateTimeKind.Utc), 'L', "Lincoln Financial Field"),
        new("Panamá",     "Croacia",    new(2026, 6, 23, 23,  0, 0, DateTimeKind.Utc), 'L', "Hard Rock Stadium"),
        new("Panamá",     "Inglaterra", new(2026, 6, 27, 21,  0, 0, DateTimeKind.Utc), 'L', "Mercedes-Benz Stadium"),
        new("Croacia",    "Ghana",      new(2026, 6, 27, 21,  0, 0, DateTimeKind.Utc), 'L', "Lincoln Financial Field"),
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
            await BackfillTeamShortCodesAsync(context, logger);
            await BackfillMatchVenuesAsync(context, logger);
            return;
        }

        var teams = Teams
            .Select(t => new Team { Name = t.Name, FlagCode = t.FlagCode, ShortCode = t.ShortCode, GroupCode = t.Group })
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
                Venue      = m.Venue,
                Stage      = MatchStage.Grupos,
                GroupCode  = m.Group,
                Status     = MatchStatus.Programado,
            })
            .ToList();

        context.Matches.AddRange(matches);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} group-stage matches.", matches.Count);
    }

    private static async Task BackfillTeamShortCodesAsync(QuinielaDbContext context, ILogger logger)
    {
        var shortCodeBySeedName = Teams.ToDictionary(t => t.Name, t => t.ShortCode, StringComparer.OrdinalIgnoreCase);
        var teamsToUpdate = await context.Teams
            .Where(t => t.ShortCode == null)
            .ToListAsync();

        if (teamsToUpdate.Count == 0) return;

        foreach (var team in teamsToUpdate)
        {
            if (shortCodeBySeedName.TryGetValue(team.Name, out var code))
                team.ShortCode = code;
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Backfilled ShortCode for {Count} teams.", teamsToUpdate.Count);
    }

    private static async Task BackfillMatchVenuesAsync(QuinielaDbContext context, ILogger logger)
    {
        var venueBySeedKey = GroupMatches
            .ToDictionary(m => (m.Home, m.Away, m.KickoffUtc), m => m.Venue);

        var matchesToUpdate = await context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.Venue == null && m.Stage == MatchStage.Grupos)
            .ToListAsync();

        if (matchesToUpdate.Count == 0) return;

        foreach (var match in matchesToUpdate)
        {
            var key = (match.HomeTeam!.Name, match.AwayTeam!.Name, match.KickoffUtc);
            if (venueBySeedKey.TryGetValue(key, out var venue))
                match.Venue = venue;
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Backfilled Venue for {Count} group-stage matches.", matchesToUpdate.Count);
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
