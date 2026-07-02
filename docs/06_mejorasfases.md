# 06 — Mejoras de Fases y Funcionalidades Adicionales

**Fecha:** 2026-07-02  
**Estado:** Pendiente  
**Contexto:** Torneo en fase eliminatoria (Dieciseisavos activos). Módulos 0–8 completos. Este documento recoge las mejoras identificadas al analizar el estado del proyecto, ordenadas por urgencia.

---

## Resumen de prioridades

| # | Hecho | Módulo | Urgencia | Esfuerzo | Impacto |
|---|:-----:|--------|----------|----------|---------|
| A | [x] | Tabs de Fases extendidas (Octavos → Final) | 🔴 Alta | ~2–3 h | Alto |
| B | [x] | Ver pronósticos de todos por partido | 🔴 Alta | ~3–4 h | Muy alto |
| C | [ ] | Bracket visual de eliminatorias | 🔴 Alta | ~6–8 h | Muy alto |
| D | [ ] | Badge de pronósticos pendientes en NavMenu | 🟡 Media | ~1–2 h | Alto |
| E | [ ] | Estadísticas personales | 🟡 Media | ~5–8 h | Medio |
| F | [ ] | Historial de posiciones en Standings | 🟡 Media | ~4–6 h | Medio |
| H | [ ] | Predicción especial "¿Quién gana el Mundial?" | 🟢 Baja | ~4–6 h | Bajo |

**Orden de implementación sugerido:** A → B → C → D → E → F → H

---

## Módulo A — Tabs de Fases extendidas (Octavos → Final)

### Objetivo

La página `/fases` tiene actualmente dos tabs fijos: **Grupos** y **Dieciseisavos**. Conforme el torneo avance a Octavos, Cuartos, Semifinal y Final, deben aparecer los tabs correspondientes automáticamente, sin necesidad de nuevas migraciones ni cambios de código.

### Contexto técnico

El enum `MatchStage` ya contempla todos los valores:
```csharp
Grupos, Dieciseisavos, Octavos, Cuartos, Semifinal, TercerLugar, Final
```

El `FaseTab` enum en `Groups/Index.razor` fue diseñado extensible. La query de `KnockoutService.GetMatchesByStageAsync` ya funciona para cualquier stage.

### Diseño de la solución

Cambiar los tabs de hardcoded a **generados dinámicamente** según los stages que tienen al menos un partido en BD.

```csharp
// Groups/Index.razor — @code
private static readonly (MatchStage Stage, string Label)[] StageOrder =
[
    (MatchStage.Grupos,        "Grupos"),
    (MatchStage.Dieciseisavos, "Dieciseisavos"),
    (MatchStage.Octavos,       "Octavos"),
    (MatchStage.Cuartos,       "Cuartos"),
    (MatchStage.Semifinal,     "Semifinal"),
    (MatchStage.TercerLugar,   "3er Lugar"),
    (MatchStage.Final,         "Final"),
];

private List<MatchStage> _activeStages = [];
private MatchStage _activeTab = MatchStage.Grupos;

protected override async Task OnInitializedAsync()
{
    // Solo mostrar tabs de stages que tienen partidos en BD
    _activeStages = await KnockoutService.GetStagesWithMatchesAsync();
    // Activar el tab del stage más avanzado con partidos en curso o próximos
    _activeTab = _activeStages.Last();
}
```

```razor
<ul class="nav nav-tabs mb-3" role="tablist">
    @foreach (var (stage, label) in StageOrder.Where(s => _activeStages.Contains(s.Stage)))
    {
        <li class="nav-item">
            <button class="nav-link @(_activeTab == stage ? "active" : "")"
                    @onclick="() => _activeTab = stage" type="button">
                @label
            </button>
        </li>
    }
</ul>

@if (_activeTab == MatchStage.Grupos)
{
    @* contenido actual de grupos *@
}
else
{
    @* lista de partidos del stage activo *@
    <KnockoutStageView Stage="_activeTab" />
}
```

### Nuevo método en KnockoutService

```csharp
// Devuelve los stages que tienen al menos 1 partido en BD, ordenados
public async Task<List<MatchStage>> GetStagesWithMatchesAsync()
{
    return await db.Matches
        .Select(m => m.Stage)
        .Distinct()
        .OrderBy(s => s)
        .ToListAsync();
}
```

### Nuevo componente compartido: KnockoutStageView.razor

Extraer la lógica actual del tab Dieciseisavos a un componente reutilizable que recibe el stage como parámetro.

```razor
@* Components/Shared/KnockoutStageView.razor *@
@code {
    [Parameter] public MatchStage Stage { get; set; }
}
```

### Archivos a modificar / crear

| Archivo | Cambio |
|---|---|
| `Services/KnockoutService.cs` | Agregar `GetStagesWithMatchesAsync` |
| `Components/Pages/Groups/Index.razor` | Tabs dinámicos + delegar a `KnockoutStageView` |
| `Components/Shared/KnockoutStageView.razor` | **Nuevo** — vista de partidos de un stage |

### Criterio de aceptación

- Con solo Grupos y Dieciseisavos en BD: 2 tabs visibles.
- Al agregar partidos de Octavos (via seed o admin): aparece automáticamente el tab Octavos sin cambiar código.
- El tab activo por defecto es el del stage más avanzado disponible.

### Estimación: 2–3 horas

---

## Módulo B — Ver pronósticos de todos por partido

### Objetivo

Una vez que un partido está finalizado, cualquier miembro de la sala puede ver qué pronosticó cada jugador (resultado e instancia en KO), y si acertó o no. Es la funcionalidad que genera más conversación entre amigos.

### Diseño de la vista

```
MÉXICO 2–1 POLONIA  •  Jun 11  •  Grupo A
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Jugador        Pronóstico    Instancia   Pts
  ─────────────────────────────────────────
  admin        🇲🇽 MEX  ✓    —            +3
  jugador1      Empate  ✗    —             0
  jugador2     🇲🇽 MEX  ✓    —            +3
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Aciertos: 2 / 3  •  Promedio: 2 pts
```

- En partidos de **grupos**: columnas Jugador / Pronóstico / Pts.
- En partidos de **KO**: columnas Jugador / Avance / Instancia / Pts (desglose con `PtsResult` y `PtsInstance`).
- Si un jugador no hizo pronóstico: mostrar `—` en gris.
- Columna Pronóstico muestra: bandera del equipo pronosticado + texto (Local/Empate/Visitante).

### Acceso a la vista

Desde dos puntos de entrada:
1. **Tab Finalizados** en `MyPredictions.razor` — botón "Ver todos" o ícono de grupo en el footer de la `MatchCard`.
2. **Tabla de Grupos** en `Groups/Index.razor` — al tocar el marcador de un partido finalizado.

Implementar como un **componente `MatchPredictionsSheet.razor`** con el mismo patrón de bottom sheet que `TeamSheet.razor`.

### Nuevo servicio: MatchPredictionsService

```csharp
public class MatchPredictionEntry
{
    public string DisplayName { get; set; } = "";
    public char? PredOutcome { get; set; }          // null = sin pronóstico
    public MatchDecidedIn? PredInstance { get; set; }
    public int PtsResult { get; set; }
    public int PtsInstance { get; set; }
    public int Points => PtsResult + PtsInstance;
    public bool HasPrediction => PredOutcome.HasValue;
}

public class MatchPredictionsSummary
{
    public Match Match { get; set; } = null!;
    public List<MatchPredictionEntry> Entries { get; set; } = [];
    public int TotalCorrect => Entries.Count(e => e.PtsResult > 0);
}

public class MatchPredictionsService(QuinielaDbContext db)
{
    public async Task<MatchPredictionsSummary> GetForMatchAsync(int matchId, int poolId)
    {
        var match = await db.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .FirstOrDefaultAsync(m => m.Id == matchId)
            ?? throw new KeyNotFoundException();

        // Todos los miembros de la sala
        var members = await db.PoolMembers
            .Include(pm => pm.User)
            .Where(pm => pm.PoolId == poolId)
            .ToListAsync();

        // Predicciones existentes para ese partido y sala
        var preds = await db.Predictions
            .Where(p => p.MatchId == matchId && p.PoolId == poolId)
            .ToDictionaryAsync(p => p.UserId);

        var entries = members.Select(m =>
        {
            preds.TryGetValue(m.UserId, out var pred);
            return new MatchPredictionEntry
            {
                DisplayName  = m.User.DisplayName,
                PredOutcome  = pred?.PredOutcome,
                PredInstance = pred?.PredInstance,
                PtsResult    = pred?.PtsResult ?? 0,
                PtsInstance  = pred?.PtsInstance ?? 0,
            };
        })
        .OrderByDescending(e => e.Points)
        .ThenBy(e => e.DisplayName)
        .ToList();

        return new MatchPredictionsSummary { Match = match, Entries = entries };
    }
}
```

### Nuevo componente: MatchPredictionsSheet.razor

Mismo patrón CSS que `TeamSheet.razor` (bottom sheet con overlay, `slideUpSheet` animation, `max-height: 78dvh`).

```razor
@* Uso en MyPredictions.razor *@
@if (_selectedMatchId.HasValue)
{
    <MatchPredictionsSheet MatchId="_selectedMatchId.Value"
                           PoolId="PoolId"
                           OnClose="() => _selectedMatchId = null" />
}
```

### Archivos a crear / modificar

| Archivo | Cambio |
|---|---|
| `Services/MatchPredictionsService.cs` | **Nuevo** |
| `Components/Shared/MatchPredictionsSheet.razor(.css)` | **Nuevo** — bottom sheet |
| `Components/Pages/Predictions/MyPredictions.razor` | Botón "Ver todos" en footer de finalizados |
| `Components/Pages/Groups/Index.razor` | Tap en marcador para abrir sheet |
| `Program.cs` | Registrar `MatchPredictionsService` |

### Criterio de aceptación

- Solo visible en partidos **finalizados**.
- Muestra todos los miembros, incluso quienes no pronosticaron.
- En KO: muestra avance e instancia por separado con el desglose de puntos.
- Cerrar tocando el overlay o el botón ×.
- Si ningún miembro pronosticó: mensaje "Nadie pronosticó este partido".

### Estimación: 3–4 horas

---

## Módulo C — Bracket visual de eliminatorias

### Objetivo

Una vista tipo copa que muestra el cuadro completo de eliminatorias (Dieciseisavos → Octavos → Cuartos → Semifinal → Final), actualizable conforme avanza el torneo. Cada cruce muestra equipos (o `SlotLabel` si aún no están definidos), marcador y resultado.

### Ruta

`/bracket` — accesible desde el NavMenu (nuevo ítem) y desde la página de Fases.

### Diseño

```
Dieciseisavos   Octavos     Cuartos   Semifinal    Final
─────────────   ───────     ───────   ─────────    ─────
MEX 2–1 POL  ─┐
               ├─ MEX ? – ? ─┐
ARG ? – ? ───┘              │
                             ├─ ? – ? ─┐
USA ? – ? ───┐              │          │
               ├─ ? – ? ────┘          ├─ FINAL
FRA ? – ? ───┘                        │
                                       ...
```

**Mobile:** scroll horizontal, cada ronda es una columna de ~140px de ancho.
**Desktop:** viewport completo, columnas de igual ancho con líneas SVG de conexión.

### Implementación

La conexión entre rondas se dibuja con SVG (`<line>` o `<path>`) generadas desde C# con las coordenadas de cada tarjeta.

```csharp
// BracketService — nuevo servicio
public class BracketRound
{
    public MatchStage Stage { get; set; }
    public string Label { get; set; } = "";
    public List<BracketMatch> Matches { get; set; } = [];
}

public class BracketMatch
{
    public Match Match { get; set; } = null!;
    public string HomeName  { get; set; } = "";   // Team.ShortCode o HomeSlotLabel
    public string AwayName  { get; set; } = "";
    public string HomFlag   { get; set; } = "";   // FlagCode o ""
    public string AwayFlag  { get; set; } = "";
    public bool   IsFinalized { get; set; }
    // Para marcar al ganador (quién avanza al siguiente cruce)
    public char?  WinnerSide  { get; set; }        // 'H' o 'A'
}

public class BracketService(QuinielaDbContext db)
{
    private static readonly MatchStage[] KoStages =
    [
        MatchStage.Dieciseisavos, MatchStage.Octavos,
        MatchStage.Cuartos, MatchStage.Semifinal,
        MatchStage.TercerLugar, MatchStage.Final
    ];

    public async Task<List<BracketRound>> GetBracketAsync()
    {
        var matches = await db.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => KoStages.Contains(m.Stage))
            .OrderBy(m => m.Stage)
            .ThenBy(m => m.BracketOrder)
            .ToListAsync();

        return KoStages
            .Where(s => matches.Any(m => m.Stage == s))
            .Select(stage => new BracketRound
            {
                Stage   = stage,
                Label   = StageLabel(stage),
                Matches = matches
                    .Where(m => m.Stage == stage)
                    .Select(BuildBracketMatch)
                    .ToList()
            })
            .ToList();
    }

    private static BracketMatch BuildBracketMatch(Match m) => new()
    {
        Match     = m,
        HomeName  = m.HomeTeam?.ShortCode ?? m.HomeSlotLabel ?? "?",
        AwayName  = m.AwayTeam?.ShortCode ?? m.AwaySlotLabel ?? "?",
        HomFlag   = m.HomeTeam?.FlagCode ?? "",
        AwayFlag  = m.AwayTeam?.FlagCode ?? "",
        IsFinalized = m.Status == MatchStatus.Finalizado,
        WinnerSide  = m.Status == MatchStatus.Finalizado
            ? (m.HomeScore > m.AwayScore ? 'H' : 'A')
            : null,
    };

    private static string StageLabel(MatchStage s) => s switch
    {
        MatchStage.Dieciseisavos => "16avos",
        MatchStage.Octavos       => "Octavos",
        MatchStage.Cuartos       => "Cuartos",
        MatchStage.Semifinal     => "Semis",
        MatchStage.TercerLugar   => "3er Lugar",
        MatchStage.Final         => "Final",
        _                        => s.ToString()
    };
}
```

### Tarjeta de cruce dentro del bracket

```razor
@* Components/Shared/BracketMatchCard.razor *@
<div class="bmc @(Match.IsFinalized ? "bmc-done" : "") @(IsWinner("H") ? "bmc-winner-home" : "")">
    <div class="bmc-team @(IsWinner("H") ? "bmc-winner" : "bmc-loser")">
        @if (!string.IsNullOrEmpty(Match.HomFlag))
        {
            <span class="fi fi-@Match.HomFlag bmc-flag"></span>
        }
        <span class="bmc-name">@Match.HomeName</span>
        @if (Match.IsFinalized)
        {
            <span class="bmc-score">@Match.Match.HomeScore</span>
        }
    </div>
    <div class="bmc-divider"></div>
    <div class="bmc-team @(IsWinner("A") ? "bmc-winner" : "bmc-loser")">
        @if (!string.IsNullOrEmpty(Match.AwayFlag))
        {
            <span class="fi fi-@Match.AwayFlag bmc-flag"></span>
        }
        <span class="bmc-name">@Match.AwayName</span>
        @if (Match.IsFinalized)
        {
            <span class="bmc-score">@Match.Match.AwayScore</span>
        }
    </div>
</div>

@code {
    [Parameter] public BracketMatch Match { get; set; } = null!;
    private bool IsWinner(string side) =>
        Match.WinnerSide.HasValue && Match.WinnerSide.Value.ToString() == side;
}
```

### CSS base del bracket

```css
/* Bracket.razor.css */
.bracket-container {
    display: flex;
    gap: 24px;
    overflow-x: auto;
    padding: 16px 8px 24px;
    align-items: flex-start;
}

.bracket-round {
    display: flex;
    flex-direction: column;
    gap: 0;
    min-width: 140px;
}

.bracket-round-label {
    font-size: 0.6rem;
    font-weight: 700;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: #64748B;
    text-align: center;
    margin-bottom: 12px;
}

/* Tarjeta de partido */
.bmc {
    background: #fff;
    border: 1px solid #E2E8F0;
    border-radius: 8px;
    overflow: hidden;
    font-size: 0.72rem;
    margin-bottom: 12px;
    transition: box-shadow 0.15s;
}

.bmc-done { border-color: #CBD5E1; }

.bmc-team {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 6px 8px;
}

.bmc-flag {
    width: 18px !important;
    height: 14px !important;
    border-radius: 2px;
    flex-shrink: 0;
}

.bmc-name {
    flex: 1;
    font-weight: 600;
    color: #1E293B;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.bmc-score {
    font-weight: 700;
    color: #1E293B;
    min-width: 16px;
    text-align: right;
}

.bmc-winner .bmc-name,
.bmc-winner .bmc-score { color: #059669; font-weight: 700; }

.bmc-loser .bmc-name,
.bmc-loser .bmc-score { color: #94A3B8; }

.bmc-divider {
    height: 1px;
    background: #F1F5F9;
    margin: 0 8px;
}
```

### NavMenu — nuevo ítem

```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="bracket">
        <span class="bi bi-bracket-nav-menu" aria-hidden="true"></span> Bracket
    </NavLink>
</div>
```

### Archivos a crear / modificar

| Archivo | Cambio |
|---|---|
| `Services/BracketService.cs` | **Nuevo** |
| `Components/Pages/Bracket/Index.razor(.css)` | **Nueva página** en `/bracket` |
| `Components/Shared/BracketMatchCard.razor(.css)` | **Nuevo componente** |
| `Components/Layout/NavMenu.razor(.css)` | Agregar ítem "Bracket" + icono SVG |
| `Program.cs` | Registrar `BracketService` |

### Criterio de aceptación

- Solo muestra rondas que tienen partidos en BD (no rondas futuras vacías).
- Partidos sin equipos definidos muestran `SlotLabel` (ej. "1A", "3° C/D/F").
- Partidos finalizados muestran marcador; el equipo ganador aparece resaltado.
- Scroll horizontal funciona en móvil sin romper el layout.
- `@page "/bracket"` protegido con `[Authorize]`.

### Estimación: 6–8 horas

---

## Módulo D — Badge de pronósticos pendientes en NavMenu

### Objetivo

Un badge numérico en el ítem "Pronosticar" del menú lateral que indique cuántos partidos próximos no tienen pronóstico del usuario en su sala activa. Evita que el usuario llegue tarde a un partido de eliminatoria.

### Diseño

```
⚽ Pronosticar  [3]
```

- Badge rojo pequeño (`badge bg-danger`) con el conteo.
- Se muestra solo cuando el conteo > 0.
- "Sala activa" = la primera sala del usuario (o la última visitada, guardada en `localStorage`).
- Se recalcula al cargar el layout. No requiere tiempo real.

### Implementación

```csharp
// PredictionService — método nuevo
public async Task<int> GetPendingCountAsync(int userId, int poolId)
{
    var now = DateTime.UtcNow;

    // Partidos futuros de la sala con equipos definidos
    var upcomingMatchIds = await db.Matches
        .Where(m => m.KickoffUtc > now
                 && m.HomeTeamId != null
                 && m.AwayTeamId != null)
        .Select(m => m.Id)
        .ToListAsync();

    // De esos, cuántos ya tienen predicción del usuario en esta sala
    var predictedCount = await db.Predictions
        .CountAsync(p => p.UserId == userId
                      && p.PoolId == poolId
                      && upcomingMatchIds.Contains(p.MatchId));

    return upcomingMatchIds.Count - predictedCount;
}
```

En `NavMenu.razor`, inyectar `PredictionService` y el `AuthenticationState` para obtener el `userId`, cargar el conteo en `OnInitializedAsync`, y mostrarlo en el ítem del menú:

```razor
<NavLink class="nav-link" href="pools/@_activePoolId/predictions">
    <span class="bi bi-ball-nav-menu" aria-hidden="true"></span>
    Pronosticar
    @if (_pendingCount > 0)
    {
        <span class="badge bg-danger ms-auto">@_pendingCount</span>
    }
</NavLink>
```

**Nota:** Para no inyectar servicios directamente en el layout (acoplamiento), se puede usar un `CascadingValue` desde `MainLayout` o un `StateContainer` singleton que el layout recargue cada vez que se navega.

### Archivos a modificar

| Archivo | Cambio |
|---|---|
| `Services/PredictionService.cs` | Agregar `GetPendingCountAsync` |
| `Components/Layout/NavMenu.razor` | Badge sobre ítem Pronosticar |
| `Components/Layout/NavMenu.razor.css` | Estilo del badge (si difiere del Bootstrap default) |

### Criterio de aceptación

- Badge visible solo cuando hay pronósticos pendientes (> 0).
- Desaparece cuando el usuario guarda su último pronóstico pendiente.
- Si el usuario no pertenece a ninguna sala: no muestra badge (evitar errores).

### Estimación: 1–2 horas

---

## Módulo E — Estadísticas personales

### Objetivo

Una página de estadísticas por jugador dentro de la sala, accesible desde Standings o desde el Perfil. Muestra rendimiento individual con métricas relevantes para el torneo actual.

### Ruta

`/pools/{poolId}/my-stats`

### Métricas a mostrar

```
┌──────────────────────────────────────────┐
│  ESTADÍSTICAS  •  jugador1  •  Sala "MX" │
├──────────────────────────────────────────┤
│  42 pts  •  Posición #2 de 8             │
├──────────────────┬───────────────────────┤
│  Aciertos        │  14 / 20  (70%)       │
│  Aciertos grupos │  12 / 18  (66%)       │
│  Aciertos KO     │   2 / 2  (100%)       │
│  Puntos instancia│   4 pts  (2 aciertos) │
├──────────────────┴───────────────────────┤
│  MEJOR RACHA     │  5 partidos seguidos  │
│  RACHA ACTUAL    │  2 partidos seguidos  │
├──────────────────────────────────────────┤
│  PUNTOS POR JORNADA (barras)             │
│  Jun 11 ████░  Jun 12 ██░  ...           │
└──────────────────────────────────────────┘
```

### Nuevo servicio: PlayerStatsService

```csharp
public class PlayerStats
{
    public int TotalPoints { get; set; }
    public int Position { get; set; }
    public int TotalMembers { get; set; }

    // Aciertos
    public int TotalPredictions { get; set; }
    public int CorrectResults { get; set; }    // PtsResult > 0
    public int CorrectInstances { get; set; }  // PtsInstance > 0 (KO)
    public int GroupPredictions { get; set; }
    public int GroupCorrect { get; set; }
    public int KoPredictions { get; set; }
    public int KoCorrect { get; set; }

    // Rachas
    public int BestStreak { get; set; }
    public int CurrentStreak { get; set; }

    // Puntos por fecha (para el mini-gráfico)
    public List<DayPoints> PointsByDay { get; set; } = [];
}

public record DayPoints(DateOnly Date, int Points);

public class PlayerStatsService(QuinielaDbContext db, StandingsService standingsService)
{
    public async Task<PlayerStats> GetAsync(int userId, int poolId)
    {
        var predictions = await db.Predictions
            .Include(p => p.Match)
            .Where(p => p.UserId == userId && p.PoolId == poolId
                     && p.Match.Status == MatchStatus.Finalizado)
            .OrderBy(p => p.Match.KickoffUtc)
            .ToListAsync();

        // Standings para posición
        var standings = await standingsService.GetStandingsAsync(poolId);
        var myRow = standings.FirstOrDefault(s => s.UserId == userId);

        // Racha: recorrer predicciones ordenadas por fecha
        int bestStreak = 0, currentStreak = 0, streak = 0;
        foreach (var p in predictions)
        {
            if (p.PtsResult > 0) { streak++; bestStreak = Math.Max(bestStreak, streak); }
            else streak = 0;
        }
        currentStreak = streak;

        // Puntos por día
        var byDay = predictions
            .GroupBy(p => DateOnly.FromDateTime(p.Match.KickoffUtc))
            .Select(g => new DayPoints(g.Key, g.Sum(p => p.Points)))
            .OrderBy(d => d.Date)
            .ToList();

        bool isKo(Prediction p) => p.Match.Stage != MatchStage.Grupos;

        return new PlayerStats
        {
            TotalPoints      = myRow?.TotalPoints ?? 0,
            Position         = (myRow is not null ? standings.IndexOf(myRow) + 1 : 0),
            TotalMembers     = standings.Count,
            TotalPredictions = predictions.Count,
            CorrectResults   = predictions.Count(p => p.PtsResult > 0),
            CorrectInstances = predictions.Count(p => p.PtsInstance > 0),
            GroupPredictions = predictions.Count(p => !isKo(p)),
            GroupCorrect     = predictions.Count(p => !isKo(p) && p.PtsResult > 0),
            KoPredictions    = predictions.Count(isKo),
            KoCorrect        = predictions.Count(p => isKo(p) && p.PtsResult > 0),
            BestStreak       = bestStreak,
            CurrentStreak    = currentStreak,
            PointsByDay      = byDay,
        };
    }
}
```

### Mini-gráfico de barras (CSS puro, sin librería)

```razor
<div class="stats-bar-chart">
    @foreach (var day in Stats.PointsByDay)
    {
        var pct = maxDayPoints > 0 ? (day.Points * 100 / maxDayPoints) : 0;
        <div class="stats-bar-col" title="@day.Date.ToString("dd/MM"): @day.Points pts">
            <div class="stats-bar" style="height: @pct%"></div>
            <span class="stats-bar-label">@day.Date.ToString("dd/MM")</span>
        </div>
    }
</div>
```

```css
.stats-bar-chart {
    display: flex;
    align-items: flex-end;
    gap: 4px;
    height: 80px;
    padding-bottom: 20px;  /* espacio para labels */
}
.stats-bar-col { display: flex; flex-direction: column; align-items: center; flex: 1; height: 100%; }
.stats-bar { width: 100%; background: var(--q-blue, #1A56DB); border-radius: 3px 3px 0 0;
             min-height: 2px; transition: height 0.4s ease; }
.stats-bar-label { font-size: 0.48rem; color: #94A3B8; margin-top: 4px; transform: rotate(-45deg); }
```

### Acceso desde Standings

En `Standings/Index.razor`, al tocar la fila del usuario actual (la que tiene el badge "TÚ"), navegar a `/pools/{poolId}/my-stats`.

### Archivos a crear / modificar

| Archivo | Cambio |
|---|---|
| `Services/PlayerStatsService.cs` | **Nuevo** |
| `Components/Pages/Stats/Index.razor(.css)` | **Nueva página** en `/pools/{poolId}/my-stats` |
| `Components/Pages/Standings/Index.razor` | Link a stats desde fila "TÚ" |
| `Program.cs` | Registrar `PlayerStatsService` |

### Criterio de aceptación

- Solo accesible por el propio usuario (validar `userId == currentUser`).
- Si no hay partidos finalizados: mensaje "Aún no hay partidos finalizados".
- % de aciertos se muestra como fracción + porcentaje.
- El mini-gráfico de barras escala correctamente con 1 a 40+ jornadas.
- Racha actual = 0 si el último partido fue fallado.

### Estimación: 5–8 horas

---

## Módulo F — Historial de posiciones en Standings

### Objetivo

Guardar un snapshot de la tabla de posiciones cada vez que se finaliza un partido, para poder mostrar cómo ha cambiado la posición de cada jugador a lo largo del torneo.

### Nuevo esquema

```csharp
// Quiniela.Data/Entities/StandingsSnapshot.cs
public class StandingsSnapshot
{
    public int Id          { get; set; }
    public int PoolId      { get; set; }
    public int MatchId     { get; set; }    // partido que disparó el recálculo
    public int UserId      { get; set; }
    public int Position    { get; set; }
    public int Points      { get; set; }
    public DateTime SavedAt { get; set; }

    public Pool   Pool  { get; set; } = null!;
    public User   User  { get; set; } = null!;
    public Match  Match { get; set; } = null!;
}
```

Migración: `AddStandingsSnapshot`

### Guardado automático desde ScoringService

Al final de `RecalculateForMatchAsync`, después de `SaveChangesAsync()`, tomar los standings actuales y guardar un snapshot **por cada sala que tiene predicciones de ese partido**:

```csharp
// ScoringService.cs — al final de RecalculateForMatchAsync
private async Task SaveSnapshotAsync(int matchId)
{
    var poolIds = await db.Predictions
        .Where(p => p.MatchId == matchId)
        .Select(p => p.PoolId)
        .Distinct()
        .ToListAsync();

    foreach (var poolId in poolIds)
    {
        var standings = await standingsService.GetStandingsAsync(poolId);
        var snapshots = standings.Select((row, idx) => new StandingsSnapshot
        {
            PoolId   = poolId,
            MatchId  = matchId,
            UserId   = row.UserId,
            Position = idx + 1,
            Points   = row.TotalPoints,
            SavedAt  = DateTime.UtcNow,
        });
        db.StandingsSnapshots.AddRange(snapshots);
    }
    await db.SaveChangesAsync();
}
```

### Visualización

En `Standings/Index.razor`, al lado del nombre de cada jugador, mostrar un indicador de cambio de posición respecto al snapshot anterior:

```
🥇 1  admin       ▲+1   48 pts
🥈 2  jugador1    ─     42 pts
🥉 3  jugador2    ▼-1   38 pts
```

- `▲+N` verde si subió.
- `▼-N` rojo si bajó.
- `─` gris si mantuvo posición o es el primer partido.

### Consideración: partidos ya finalizados en BD

El diseño base solo captura snapshots a partir de que el módulo está activo. Si ya existen partidos finalizados en BD antes de la migración, la tabla `StandingsSnapshots` quedará vacía y los indicadores ▲/▼/─ mostrarán `─` para todos indefinidamente.

**Solución: backfill en la migración**

Al correr `AddStandingsSnapshot`, ejecutar un backfill que recorra los partidos ya finalizados ordenados por `KickoffUtc` y genere un snapshot de las standings **actuales** (no históricas) asociado al partido más antiguo, como "línea base":

```csharp
// En la migración — método Up, después de crear la tabla
// Obtener el partido finalizado más antiguo por sala
// y guardar las standings actuales como snapshot inicial

// (ejecutar con SQL directo desde la migración o con un seed method)
```

> **Limitación conocida:** reconstruir posiciones históricas exactas requeriría recalcular puntos estado a estado, lo cual es costoso. El backfill guarda la posición actual como punto de partida; a partir del siguiente partido finalizado, los deltas serán reales.

Además, en `GetLastSnapshotPositionsAsync`, si no existe snapshot previo para una sala, devolver un diccionario vacío para que el componente muestre `─` en lugar de fallar:

```csharp
public async Task<Dictionary<int, int>> GetLastSnapshotPositionsAsync(int poolId)
{
    var lastMatchId = await db.StandingsSnapshots
        .Where(s => s.PoolId == poolId)
        .OrderByDescending(s => s.SavedAt)
        .Select(s => s.MatchId)
        .FirstOrDefaultAsync();

    if (lastMatchId == 0) return [];   // no hay snapshots aún → todo ─

    return await db.StandingsSnapshots
        .Where(s => s.PoolId == poolId && s.MatchId == lastMatchId)
        .ToDictionaryAsync(s => s.UserId, s => s.Position);
}
```

### Archivos a crear / modificar

| Archivo | Cambio |
|---|---|
| `Quiniela.Data/Entities/StandingsSnapshot.cs` | **Nueva entidad** |
| `QuinielaDbContext.cs` | `DbSet<StandingsSnapshot>` + config FK |
| `Migrations/` | `AddStandingsSnapshot` + backfill de línea base |
| `Services/ScoringService.cs` | Llamar `SaveSnapshotAsync` al final del recálculo |
| `Components/Pages/Standings/Index.razor` | Indicadores ▲/▼/─ por jugador |
| `Services/StandingsService.cs` | Método `GetLastSnapshotPositionsAsync(poolId)` |

### Criterio de aceptación

- Después de capturar el primer resultado: todos los jugadores tienen posición y el indicador es `─`.
- Después del segundo resultado: se compara contra el snapshot anterior y aparecen ▲/▼/─ correctamente.
- En caso de empate de posición: mostrar `─` (misma posición compartida).
- La tabla `StandingsSnapshots` crece linealmente: `N_miembros × N_partidos_finalizados` filas.

### Estimación: 4–6 horas

---

## Módulo H — Predicción especial "¿Quién gana el Mundial?"

### Objetivo

Una predicción especial one-shot por sala: cada jugador elige al campeón del torneo. Se cierra en un momento configurable por el admin (antes del inicio de Octavos, por defecto). Vale puntos fijos al acertar.

### Decisiones de producto

| Tema | Decisión sugerida |
|------|-------------------|
| ¿Cuándo se cierra? | Al inicio del primer partido de Octavos (configurable) |
| Puntos por acierto | 10 pts fijos (configurable en `Pool`, nuevo campo `PtsChampion`) |
| ¿Se puede cambiar? | Sí, hasta el cierre |
| Granularidad | Por sala (cada sala tiene su campeón, igual que pronósticos normales) |
| Recálculo | Manual desde admin, o automático al capturar el resultado de la Final |

### Esquema nuevo

```csharp
// Quiniela.Data/Entities/ChampionPrediction.cs
public class ChampionPrediction
{
    public int Id        { get; set; }
    public int UserId    { get; set; }
    public int PoolId    { get; set; }
    public int TeamId    { get; set; }        // equipo pronosticado
    public int Points    { get; set; }        // 0 hasta que se resuelva la Final
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User  User  { get; set; } = null!;
    public Pool  Pool  { get; set; } = null!;
    public Team  Team  { get; set; } = null!;
}
```

Agregar en `Pool`:
```csharp
public int  PtsChampion       { get; set; } = 10;   // puntos por acertar al campeón
public DateTime? ChampionDeadline { get; set; }     // NULL = abierto
```

Migración: `AddChampionPrediction`

### Vista

`/pools/{poolId}/champion` — lista de selecciones disponibles (solo equipos clasificados a Octavos si el deadline no pasó, o todos si es antes), con botón de selección y confirmación. Al guardar, muestra "Tu pronóstico: 🇲🇽 México".

En Standings, una columna o fila extra "Pronóstico campeón" que muestra el equipo elegido por cada jugador (visible para todos en la sala).

### Integración con Scoring

Cuando se finaliza el partido de la Final, `ScoringService` llama a un método que compara `ChampionPrediction.TeamId` con el ganador de la Final y asigna `PtsChampion` a quien acertó.

### Archivos a crear / modificar

| Archivo | Cambio |
|---|---|
| `Entities/ChampionPrediction.cs` | **Nueva entidad** |
| `Entities/Pool.cs` | + `PtsChampion`, `ChampionDeadline` |
| `Migrations/` | `AddChampionPrediction` |
| `Services/ChampionService.cs` | **Nuevo** — upsert + recálculo |
| `Components/Pages/Champion/Index.razor` | **Nueva página** |
| `Services/ScoringService.cs` | Recálculo tras partido de Final |
| `Program.cs` | Registrar `ChampionService` |

### Criterio de aceptación

- Antes del `ChampionDeadline`: el usuario puede elegir y cambiar su campeón.
- Después del deadline: solo lectura.
- Si el admin no configura deadline: queda abierto hasta que se resuelva.
- Al finalizar la Final: se calculan los puntos y se suman al total de cada jugador.

### Estimación: 4–6 horas

---

## Resumen de archivos nuevos del documento completo

| Archivo | Módulo |
|---|---|
| `Services/BracketService.cs` | C |
| `Services/MatchPredictionsService.cs` | B |
| `Services/PlayerStatsService.cs` | E |
| `Services/ChampionService.cs` | H |
| `Entities/StandingsSnapshot.cs` | F |
| `Entities/ChampionPrediction.cs` | H |
| `Components/Pages/Bracket/Index.razor(.css)` | C |
| `Components/Pages/Stats/Index.razor(.css)` | E |
| `Components/Pages/Champion/Index.razor` | H |
| `Components/Shared/BracketMatchCard.razor(.css)` | C |
| `Components/Shared/KnockoutStageView.razor` | A |
| `Components/Shared/MatchPredictionsSheet.razor(.css)` | B |

---

## Orden de implementación sugerido

```
A (tabs dinámicos, 2h)
  └─> B (ver pronósticos por partido, 4h)
        └─> C (bracket visual, 8h)
              └─> D (badge pendientes, 1h)
                    └─> E (stats personales, 6h)
                          └─> F (historial posiciones, 5h)
                                └─> H (predicción campeón, 5h)
```

Total estimado: **~30–43 horas** de trabajo.
