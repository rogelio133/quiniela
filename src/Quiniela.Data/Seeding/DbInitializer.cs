using System.Text.Json;
using System.Text.Json.Serialization;
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

    private record KnockoutFile(List<KnockoutMatchJson> Partidos);
    private record MundialFile(List<SeleccionJson> Selecciones);
    private record SeleccionJson(
        string Nombre, string Abreviacion,
        string Director_Tecnico, string Dato_Curioso,
        List<JugadorJson> Jugadores,
        List<HistorialMundialJson> Historial_Mundiales);
    private record JugadorJson(string Nombre, string Posicion);
    private record HistorialMundialJson(string Mundial, string Posicion);
    private record KnockoutMatchJson(
        int Match_Id, DateTime Fecha_Utc, string Local, string Visita, string Venue);

    private record MatchesFile(
        [property: JsonPropertyName("mundial_2026_octavos_final")] List<MatchEntry> Octavos,
        [property: JsonPropertyName("mundial_2026_cuartos_final")] List<MatchEntry> Cuartos,
        [property: JsonPropertyName("mundial_2026_semifinal")] List<MatchEntry> Semifinal,
        [property: JsonPropertyName("mundial_2026_tercer_lugar")] List<MatchEntry> TercerLugar,
        [property: JsonPropertyName("mundial_2026_final")] List<MatchEntry> Final);
    private record MatchEntry(
        int Partido, DateTime Fecha_Utc, string Sede,
        string? Equipo_Local, string? Equipo_Visitante, string? Nota);

    // Nombres del JSON que difieren de los sembrados en BD (normalización mínima).
    private static readonly Dictionary<string, string> KnockoutNameFix =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["R.D. del Congo"] = "Congo RD",
        };

    private static string NormalizeKnockoutName(string name) => KnockoutNameFix.GetValueOrDefault(name, name);

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
        await SeedDieciseisavosAsync(context, logger);
        await SeedOctavosAFinalAsync(context, logger);
        await BackfillBracketOrderAsync(context, logger);
        await SeedTeamInfoAsync(context, logger);
        await SeedHistorialAsync(context, logger);
        await SeedUsersAsync(userManager, roleManager, configuration, logger);
    }

    private static async Task SeedDieciseisavosAsync(QuinielaDbContext context, ILogger logger)
    {
        if (await context.Matches.AnyAsync(m => m.Stage == MatchStage.Dieciseisavos))
            return;

        var file = await LoadDieciseisavosFileAsync();
        var matchesFile = await LoadMatchesFileAsync();
        var orderByPartido = ComputeBracketOrders(matchesFile, file);

        var teamsByName = await context.Teams.ToDictionaryAsync(t => t.Name, StringComparer.OrdinalIgnoreCase);

        int Resolve(string name)
        {
            var key = KnockoutNameFix.GetValueOrDefault(name, name);
            return teamsByName.TryGetValue(key, out var t)
                ? t.Id
                : throw new InvalidOperationException($"Equipo de dieciseisavos no encontrado en BD: '{name}'.");
        }

        var matches = file.Partidos
            .Select(p => new Match
            {
                HomeTeamId = Resolve(p.Local),
                AwayTeamId = Resolve(p.Visita),
                KickoffUtc = DateTime.SpecifyKind(p.Fecha_Utc, DateTimeKind.Utc),
                Venue = p.Venue.Split(',')[0].Trim(),
                Stage = MatchStage.Dieciseisavos,
                BracketOrder = orderByPartido[p.Match_Id],
                Status = MatchStatus.Programado,
            })
            .ToList();

        context.Matches.AddRange(matches);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} round-of-32 matches from JSON.", matches.Count);
    }

    private static async Task<MatchesFile> LoadMatchesFileAsync()
    {
        var assembly = typeof(DbInitializer).Assembly;
        await using var stream = assembly.GetManifestResourceStream(
            "Quiniela.Data.Seeding.Data.matches.json")
            ?? throw new InvalidOperationException("No se encontró matches.json embebido.");

        return await JsonSerializer.DeserializeAsync<MatchesFile>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("matches.json inválido o vacío.");
    }

    private static async Task<KnockoutFile> LoadDieciseisavosFileAsync()
    {
        var assembly = typeof(DbInitializer).Assembly;
        await using var stream = assembly.GetManifestResourceStream(
            "Quiniela.Data.Seeding.Data.mundial2026_dieciseisavos.json")
            ?? throw new InvalidOperationException("No se encontró el JSON de dieciseisavos embebido.");

        return await JsonSerializer.DeserializeAsync<KnockoutFile>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("JSON de dieciseisavos inválido o vacío.");
    }

    // Divide "X vs. Y" respetando paréntesis anidados (ej. "Ganador (Portugal vs. Croacia) vs. Ganador (España vs. Austria)").
    private static (string Home, string Away) SplitNota(string nota)
    {
        int depth = 0;
        for (int i = 0; i < nota.Length - 4; i++)
        {
            if (nota[i] == '(') depth++;
            else if (nota[i] == ')') depth--;
            else if (depth == 0 && nota.AsSpan(i, 5).SequenceEqual(" vs. "))
                return (nota[..i], nota[(i + 5)..]);
        }
        return (nota, nota);
    }

    // Extrae los números de "Partido NN" referenciados en una nota, en el orden en que aparecen
    // (ej. "Ganador Partido 89 vs. Ganador Partido 90" -> [89, 90]).
    private static List<int> ExtractPartidoRefs(string? nota) =>
        nota is null
            ? []
            : System.Text.RegularExpressions.Regex.Matches(nota, @"Partido (\d+)")
                .Select(m => int.Parse(m.Groups[1].Value))
                .ToList();

    // Deriva el BracketOrder real de cada partido a partir del árbol de eliminatorias
    // (no del número de partido FIFA, que no respeta la geometría del cuadro).
    // Se recorre de arriba hacia abajo: Final (1 partido) -> Semifinal -> Cuartos -> Octavos,
    // usando las referencias "Ganador/Perdedor Partido NN" de cada nota para saber qué dos
    // partidos de la ronda anterior deben quedar visualmente adyacentes.
    //
    // El límite Octavos -> Dieciseisavos no usa números de partido (las notas de Octavos
    // referencian nombres de equipo, no "Partido NN"), así que se resuelve con un matching
    // por nombre de equipo contra el archivo de dieciseisavos.
    private static Dictionary<int, int> ComputeBracketOrders(MatchesFile file, KnockoutFile dieciseisavos)
    {
        var orderByPartido = new Dictionary<int, int>
        {
            [file.Final.Single().Partido] = 1,
            [file.TercerLugar.Single().Partido] = 1,
        };

        List<MatchEntry>[] chain = [file.Final, file.Semifinal, file.Cuartos];

        foreach (var parentEntries in chain)
        {
            int pos = 1;
            foreach (var parent in parentEntries.OrderBy(p => orderByPartido[p.Partido]))
            {
                foreach (var refPartido in ExtractPartidoRefs(parent.Nota))
                    orderByPartido[refPartido] = pos++;
            }
        }

        var dieciseisavoMatchIdByTeam = dieciseisavos.Partidos
            .SelectMany(p => new[] { (Team: p.Local, p.Match_Id), (Team: p.Visita, p.Match_Id) })
            .ToDictionary(x => x.Team, x => x.Match_Id, StringComparer.OrdinalIgnoreCase);

        foreach (var octavo in file.Octavos.OrderBy(p => orderByPartido[p.Partido]))
        {
            var pos = orderByPartido[octavo.Partido];
            var (homeTeam, awayTeam) = octavo.Equipo_Local is not null
                ? (octavo.Equipo_Local, octavo.Equipo_Visitante!)
                : SplitTeamsFromNota(octavo.Nota!);

            if (dieciseisavoMatchIdByTeam.TryGetValue(homeTeam, out var homeMatchId))
                orderByPartido[homeMatchId] = 2 * pos - 1;
            if (dieciseisavoMatchIdByTeam.TryGetValue(awayTeam, out var awayMatchId))
                orderByPartido[awayMatchId] = 2 * pos;
        }

        return orderByPartido;
    }

    // Extrae un nombre de equipo representativo de cada lado de una nota de Octavos, ej.
    // "Ganador (Portugal vs. Croacia) vs. Ganador (España vs. Austria)" -> ("Portugal", "España").
    // Cualquiera de los dos equipos de cada grupo sirve para identificar el partido de
    // dieciseisavos de origen, ya que ambos pertenecen al mismo Match_Id.
    private static (string Home, string Away) SplitTeamsFromNota(string nota)
    {
        var (homeLabel, awayLabel) = SplitNota(nota);
        return (ExtractAnyTeamName(homeLabel), ExtractAnyTeamName(awayLabel));
    }

    private static string ExtractAnyTeamName(string label)
    {
        var start = label.IndexOf('(');
        if (start < 0) return label.Trim();

        var end = label.IndexOf(')', start);
        var (team, _) = SplitNota(label[(start + 1)..end]);
        return team.Trim();
    }

    private static async Task SeedOctavosAFinalAsync(QuinielaDbContext context, ILogger logger)
    {
        if (await context.Matches.AnyAsync(m => m.Stage == MatchStage.Octavos))
            return;

        var file = await LoadMatchesFileAsync();
        var dieciseisavos = await LoadDieciseisavosFileAsync();

        var teamsByName = await context.Teams.ToDictionaryAsync(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var orderByPartido = ComputeBracketOrders(file, dieciseisavos);

        int? Resolve(string? name)
        {
            if (name is null) return null;
            var key = KnockoutNameFix.GetValueOrDefault(name, name);
            return teamsByName.TryGetValue(key, out var t)
                ? t.Id
                : throw new InvalidOperationException($"Equipo no encontrado en BD: '{name}'.");
        }

        (List<MatchEntry> Entries, MatchStage Stage)[] groups =
        [
            (file.Octavos,     MatchStage.Octavos),
            (file.Cuartos,     MatchStage.Cuartos),
            (file.Semifinal,   MatchStage.Semifinal),
            (file.TercerLugar, MatchStage.TercerLugar),
            (file.Final,       MatchStage.Final),
        ];

        var matches = groups.SelectMany(g => g.Entries
            .Select(p =>
            {
                var (homeLabel, awayLabel) = p.Nota is not null ? SplitNota(p.Nota) : (null, null);
                return new Match
                {
                    HomeTeamId    = Resolve(p.Equipo_Local),
                    AwayTeamId    = Resolve(p.Equipo_Visitante),
                    HomeSlotLabel = p.Equipo_Local is null ? homeLabel : null,
                    AwaySlotLabel = p.Equipo_Visitante is null ? awayLabel : null,
                    KickoffUtc    = DateTime.SpecifyKind(p.Fecha_Utc, DateTimeKind.Utc),
                    Venue         = p.Sede.Split(',')[0].Trim(),
                    Stage         = g.Stage,
                    BracketOrder  = orderByPartido[p.Partido],
                    Status        = MatchStatus.Programado,
                };
            }))
            .ToList();

        context.Matches.AddRange(matches);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} matches (Octavos→Final) from matches.json.", matches.Count);
    }

    // Corrige el BracketOrder de partidos ya sembrados (Dieciseisavos→Final) si no coincide con
    // el árbol de eliminatorias derivado de matches.json + mundial2026_dieciseisavos.json.
    // Necesario porque un seed previo a este fix asignaba el orden de Octavos→Final por número
    // de partido FIFA ascendente (no respeta la geometría del cuadro) y el de Dieciseisavos por
    // fecha/hora de kickoff (sin relación con el árbol de eliminación) — ver ComputeBracketOrders.
    private static async Task BackfillBracketOrderAsync(QuinielaDbContext context, ILogger logger)
    {
        var file = await LoadMatchesFileAsync();
        var dieciseisavos = await LoadDieciseisavosFileAsync();
        var orderByPartido = ComputeBracketOrders(file, dieciseisavos);

        (List<MatchEntry> Entries, MatchStage Stage)[] groups =
        [
            (file.Octavos,     MatchStage.Octavos),
            (file.Cuartos,     MatchStage.Cuartos),
            (file.Semifinal,   MatchStage.Semifinal),
            (file.TercerLugar, MatchStage.TercerLugar),
            (file.Final,       MatchStage.Final),
        ];

        // Los partidos de Octavos→Final en BD no guardan el número de partido FIFA; se
        // identifican por (Stage, KickoffUtc), que es único dentro de matches.json.
        var orderByStageAndKickoff = groups
            .SelectMany(g => g.Entries.Select(e => (
                g.Stage,
                Kickoff: DateTime.SpecifyKind(e.Fecha_Utc, DateTimeKind.Utc),
                Order: orderByPartido[e.Partido])))
            .ToDictionary(x => (x.Stage, x.Kickoff), x => x.Order);

        var matches = await context.Matches
            .Where(m => m.Stage == MatchStage.Octavos || m.Stage == MatchStage.Cuartos
                     || m.Stage == MatchStage.Semifinal || m.Stage == MatchStage.TercerLugar
                     || m.Stage == MatchStage.Final)
            .ToListAsync();

        int updated = 0;
        foreach (var m in matches)
        {
            if (orderByStageAndKickoff.TryGetValue((m.Stage, m.KickoffUtc), out var order)
                && m.BracketOrder != order)
            {
                m.BracketOrder = order;
                updated++;
            }
        }

        // Dieciseisavos: ambos equipos siempre están definidos (no hay placeholders en esta
        // ronda), así que se identifica por (equipo local, equipo visitante) en vez de
        // KickoffUtc — más robusto ante correcciones de horario hechas después del seed
        // original (KickoffUtc puede haberse editado manualmente y ya no coincidir con el JSON).
        var orderByTeamPair = dieciseisavos.Partidos.ToDictionary(
            p => (Local: NormalizeKnockoutName(p.Local), Visita: NormalizeKnockoutName(p.Visita)),
            p => orderByPartido[p.Match_Id]);

        var dieciseisavosMatches = await context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.Stage == MatchStage.Dieciseisavos)
            .ToListAsync();

        foreach (var m in dieciseisavosMatches)
        {
            if (orderByTeamPair.TryGetValue((m.HomeTeam!.Name, m.AwayTeam!.Name), out var order)
                && m.BracketOrder != order)
            {
                m.BracketOrder = order;
                updated++;
            }
        }

        if (updated == 0) return;

        await context.SaveChangesAsync();
        logger.LogInformation("Backfilled BracketOrder for {Count} matches (Dieciseisavos→Final).", updated);
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

    private static async Task SeedHistorialAsync(QuinielaDbContext context, ILogger logger)
    {
        if (await context.HistorialMundiales.AnyAsync()) return;

        var assembly = typeof(DbInitializer).Assembly;
        await using var stream = assembly.GetManifestResourceStream(
            "Quiniela.Data.Seeding.Data.mundial_2026_2.json")
            ?? throw new InvalidOperationException("No se encontró el JSON mundial_2026_2 embebido.");

        var file = await JsonSerializer.DeserializeAsync<MundialFile>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("JSON mundial_2026_2 inválido o vacío.");

        var teamsByShortCode = await context.Teams
            .ToDictionaryAsync(t => t.ShortCode ?? "", StringComparer.OrdinalIgnoreCase);

        int added = 0;
        foreach (var sel in file.Selecciones)
        {
            if (!teamsByShortCode.TryGetValue(sel.Abreviacion, out var team))
                continue;

            var registros = sel.Historial_Mundiales.Select(h => new HistorialMundial
            {
                TeamId   = team.Id,
                Mundial  = h.Mundial,
                Posicion = h.Posicion,
            }).ToList();

            context.HistorialMundiales.AddRange(registros);
            added += registros.Count;
        }

        await context.SaveChangesAsync();
        logger.LogInformation("SeedHistorial: {Count} registros de historial mundialista insertados.", added);
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

    private static async Task SeedTeamInfoAsync(QuinielaDbContext context, ILogger logger)
    {
        var alreadySeeded = await context.Teams.AnyAsync(t => t.DatoCurioso != null);
        if (alreadySeeded) return;

        var assembly = typeof(DbInitializer).Assembly;
        await using var stream = assembly.GetManifestResourceStream(
            "Quiniela.Data.Seeding.Data.mundial_2026_2.json")
            ?? throw new InvalidOperationException("No se encontró el JSON mundial_2026_2 embebido.");

        var file = await JsonSerializer.DeserializeAsync<MundialFile>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("JSON mundial_2026_2 inválido o vacío.");

        var teamsByShortCode = await context.Teams
            .ToDictionaryAsync(t => t.ShortCode ?? "", StringComparer.OrdinalIgnoreCase);

        int teamsUpdated = 0;
        int jugadoresAdded = 0;

        foreach (var sel in file.Selecciones)
        {
            if (!teamsByShortCode.TryGetValue(sel.Abreviacion, out var team))
            {
                logger.LogWarning("SeedTeamInfo: no se encontró equipo con ShortCode '{Code}'.", sel.Abreviacion);
                continue;
            }

            team.DatoCurioso = sel.Dato_Curioso;
            team.DirectorTecnico = sel.Director_Tecnico;

            var jugadores = sel.Jugadores.Select(j => new Jugador
            {
                TeamId   = team.Id,
                Nombre   = j.Nombre,
                Posicion = j.Posicion,
            }).ToList();

            context.Jugadores.AddRange(jugadores);
            jugadoresAdded += jugadores.Count;
            teamsUpdated++;
        }

        await context.SaveChangesAsync();
        logger.LogInformation("SeedTeamInfo: actualizados {Teams} equipos, {Players} jugadores.", teamsUpdated, jugadoresAdded);
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
