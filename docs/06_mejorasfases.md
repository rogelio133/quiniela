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
| C | [x] | Bracket visual de eliminatorias | 🔴 Alta | ~6–8 h | Muy alto |
| C1 | [x] | Seed de partidos Octavos → Final (datos FIFA) | 🔴 Alta | ~2–3 h + datos | Muy alto |
| C2 | [x] | Origen del cruce visible en tab Bracket del admin | 🟡 Media | ~0.5–1 h | Medio |
| D | [x] | Badge de pronósticos pendientes en NavMenu | 🟡 Media | ~1–2 h | Alto |
| E | [x] | Estadísticas personales | 🟡 Media | ~5–8 h | Medio |
| F | [x] | Historial de posiciones en Standings | 🟡 Media | ~4–6 h | Medio |
| H | [x] | Predicción especial "¿Quién gana el Mundial?" | 🟢 Baja | ~5–7 h | Bajo |

**Orden de implementación sugerido:** A → B → C → C1 → C2 → D → E → F → H

> **Nota:** C1/C2 son la continuación natural de C — sin ellos, `/bracket` y el tab "Fases" solo tienen datos reales hasta Dieciseisavos; Octavos→Final existen en el diseño pero no en BD.

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

## Módulo C1 — Seed de partidos Octavos → Final (datos oficiales FIFA)

### Objetivo

Cargar en BD los partidos de Octavos, Cuartos, Semifinal, Tercer Lugar y Final con **sede y hora UTC oficiales de FIFA**, para que los módulos A (tabs de fases) y C (bracket visual) —ya implementados y agnósticos de `Stage`— muestren el cuadro completo aunque los equipos de rondas futuras aún no estén definidos. El seed debe fijar también los cruces de Octavos que **ya se conocen al día de hoy** (2026-07-02), a partir de los partidos de Dieciseisavos ya jugados.

### Contexto técnico (verificado en el código actual)

- El schema ya soporta esto **sin cambios**: `Match.HomeSlotLabel` / `AwaySlotLabel` (texto libre para cruces sin equipo aún) y `BracketOrder` existen desde la migración `Add2ndPhaseSupport` (Módulo 8), pero **nunca se han usado** — Dieciseisavos se sembró con los 16 equipos ya conocidos, sin placeholders.
- El precedente a seguir es `DbInitializer.SeedDieciseisavosAsync` (`src/Quiniela.Data/Seeding/DbInitializer.cs:224`): lee un JSON embebido y lo convierte en filas `Match`. C1 sigue el mismo patrón — **no** una migración EF Core con `HasData`/SQL crudo, para poder corregir sedes/horarios sin generar una migración nueva cada vez que FIFA ajusta el calendario.
- `PredictionService.GetUpcomingMatchesAsync` y `GetAllMatchesAsync` (`src/Quiniela.Web/Services/PredictionService.cs:19,32`) **ya filtran** `m.HomeTeamId != null && m.AwayTeamId != null`. Esto ya protege pronosticar / mis pronósticos **sin cambios de código**: cualquier partido de Octavos+ sembrado como placeholder (sin equipos) simplemente no aparece hasta que el admin le asigne ambos equipos (ver C2).
- El tab "Bracket" del admin, la vista `/fases` (Módulo A) y `/bracket` (Módulo C) ya son agnósticos de `MatchStage` — no requieren cambios de código para mostrar Octavos+ una vez que existan filas en BD.

### Numeración de partidos FIFA (referencia para armar el JSON)

El bracket de 48 equipos de FIFA 2026 numera los partidos 73–104:

| Rango | Fase | # Partidos |
|---|---|---|
| 73–88 | Dieciseisavos (ya sembrado) | 16 |
| 89–96 | Octavos | 8 |
| 97–100 | Cuartos | 4 |
| 101–102 | Semifinal | 2 |
| 103 | Tercer Lugar | 1 |
| 104 | Final | 1 |

### Fuente de datos

**Ya aportada por el usuario**: `matches.json` (raíz del repo). Cubre los 16 partidos (Octavos 89–96, Cuartos 97–100, Semifinal 101–102, Tercer Lugar 103, Final 104), agrupados por fase, con sede y hora UTC oficiales. En Octavos, 5 de los 8 cruces ya traen `equipo_local`/`equipo_visitante` definidos (Paraguay–Francia, Canadá–Marruecos, Brasil–Noruega, México–Inglaterra, Estados Unidos–Bélgica); los otros 3 (partidos 93, 95, 96) y todo Cuartos→Final vienen con equipos en `null` y una `nota` en texto libre describiendo el cruce.

Este archivo se moverá a `Quiniela.Data/Seeding/Data/matches.json` y se marcará como `EmbeddedResource`, mismo mecanismo que `mundial2026_dieciseisavos.json`.

### Formato real de `matches.json`

```json
{
  "mundial_2026_octavos_final": [
    {
      "partido": 89,
      "fecha_utc": "2026-07-04T21:00:00Z",
      "sede": "Philadelphia Stadium (Lincoln Financial Field), Filadelfia, EE.UU.",
      "equipo_local": "Paraguay",
      "equipo_visitante": "Francia"
    },
    {
      "partido": 93,
      "fecha_utc": "2026-07-06T19:00:00Z",
      "sede": "Dallas Stadium (AT&T Stadium), Arlington, EE.UU.",
      "equipo_local": null,
      "equipo_visitante": null,
      "nota": "Ganador (Portugal vs. Croacia) vs. Ganador (España vs. Austria)"
    }
  ],
  "mundial_2026_cuartos_final": [
    {
      "partido": 97,
      "fecha_utc": "2026-07-09T20:00:00Z",
      "sede": "Boston Stadium (Gillette Stadium), Foxborough, EE.UU.",
      "equipo_local": null,
      "equipo_visitante": null,
      "nota": "Ganador Partido 89 vs. Ganador Partido 90"
    }
  ],
  "mundial_2026_semifinal": [ /* … */ ],
  "mundial_2026_tercer_lugar": [ /* … */ ],
  "mundial_2026_final": [ /* … */ ]
}
```

- Cada clave de primer nivel (`mundial_2026_octavos_final`, `_cuartos_final`, `_semifinal`, `_tercer_lugar`, `_final`) mapea 1:1 a un `MatchStage` (`Octavos`, `Cuartos`, `Semifinal`, `TercerLugar`, `Final`).
- Si `equipo_local`/`equipo_visitante` no son `null`: se resuelven a `HomeTeamId`/`AwayTeamId`, igual que en Dieciseisavos.
- Si son `null`: el campo `nota` (un solo string que describe ambos orígenes, ej. `"Ganador Partido 89 vs. Ganador Partido 90"` o `"Ganador (Portugal vs. Croacia) vs. Ganador (España vs. Austria)"`) se **divide en dos mitades** para poblar `HomeSlotLabel`/`AwaySlotLabel` por separado. La división no puede ser un `Split(" vs. ")` ingenuo porque algunas notas tienen `" vs. "` anidado dentro de paréntesis (ej. el caso de Portugal/Croacia arriba) — hay que partir solo en el `" vs. "` de nivel superior (fuera de paréntesis).
- `sede` sigue el mismo formato `"Nombre oficial (Nombre alterno), Ciudad, País"` que Dieciseisavos → se toma el primer segmento antes de la coma, igual que ya hace `SeedDieciseisavosAsync`.
- `partido` no se persiste (no hay columna para él); solo determina el orden dentro de cada fase (`BracketOrder`).

### Helper para partir `nota` en Local/Visitante

```csharp
// DbInitializer.cs — divide "X vs. Y" respetando paréntesis anidados
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
    return (nota, nota); // fallback si no se encuentra un separador de nivel superior
}
```

### Nuevo método en DbInitializer

```csharp
// DbInitializer.cs
private static async Task SeedOctavosAFinalAsync(QuinielaDbContext context, ILogger logger)
{
    if (await context.Matches.AnyAsync(m => m.Stage == MatchStage.Octavos))
        return;

    var assembly = typeof(DbInitializer).Assembly;
    await using var stream = assembly.GetManifestResourceStream(
        "Quiniela.Data.Seeding.Data.matches.json")
        ?? throw new InvalidOperationException("No se encontró matches.json embebido.");

    var file = await JsonSerializer.DeserializeAsync<MatchesFile>(stream,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("matches.json inválido o vacío.");

    var teamsByName = await context.Teams.ToDictionaryAsync(t => t.Name, StringComparer.OrdinalIgnoreCase);

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
        .OrderBy(p => p.Partido)
        .Select((p, i) =>
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
                BracketOrder  = i + 1,
                Status        = MatchStatus.Programado,
            };
        }))
        .ToList();

    context.Matches.AddRange(matches);
    await context.SaveChangesAsync();
    logger.LogInformation("Seeded {Count} matches (Octavos→Final) from matches.json.", matches.Count);
}
```

`MatchesFile`/`MatchEntry` son records nuevos que reflejan el esquema del JSON (propiedades `Mundial_2026_Octavos_Final`, `Mundial_2026_Cuartos_Final`, etc. — o usar `[JsonPropertyName]` explícito por claridad).

Registrar la llamada en `SeedAsync`, justo después de `SeedDieciseisavosAsync`:

```csharp
await SeedDieciseisavosAsync(context, logger);
await SeedOctavosAFinalAsync(context, logger);   // nuevo
```

### Archivos a crear / modificar

| Archivo | Cambio |
|---|---|
| `matches.json` (raíz) → `Quiniela.Data/Seeding/Data/matches.json` | **Mover** — ya aportado por el usuario |
| `Quiniela.Data/Seeding/DbInitializer.cs` | Nuevo método `SeedOctavosAFinalAsync` + `SplitNota` + registro en `SeedAsync` + records `MatchesFile`/`MatchEntry` |
| `Quiniela.Data.csproj` | Agregar `matches.json` como `EmbeddedResource` (igual que el de Dieciseisavos) |

### Criterio de aceptación

- Al iniciar la app con BD sin partidos de Octavos, se crean automáticamente 8+4+2+1+1 = 16 partidos nuevos con `Venue`/`KickoffUtc` correctos.
- Los partidos de Octavos con equipos ya conocidos hoy quedan con `HomeTeamId`/`AwayTeamId` poblados (sin placeholder).
- El resto de Octavos y todo Cuartos/Semifinal/Final quedan con `HomeSlotLabel`/`AwaySlotLabel` poblados y `HomeTeamId`/`AwayTeamId` en `null`.
- **Regresión a verificar (sin cambios de código):** `/pools/{id}/predictions` y `/pools/{id}/my-predictions` no muestran ningún partido de Octavos+ hasta que el admin le asigne ambos equipos — ya cubierto por el filtro existente en `PredictionService`.
- El tab "Fases" (Módulo A) y `/bracket` (Módulo C) muestran las nuevas rondas automáticamente, con `SlotLabel` donde aplique.
- Reiniciar la app con los partidos ya sembrados no duplica filas (guard `AnyAsync(Stage == Octavos)`).

### Estimación: 2–3 horas de código + tiempo variable para recopilar/verificar los datos oficiales de FIFA

---

## Módulo C2 — Origen del cruce visible en el tab Bracket del admin

### Objetivo

Cuando el admin asigna equipos a un partido de Octavos+ en el tab "Bracket" (`/admin`), debe quedar claro de dónde viene cada cupo (p. ej. "Ganador Partido 89" o "Ganador (Portugal vs. Croacia)"), para no adivinar qué casilla corresponde a cuál cruce.

### Hallazgo clave: gran parte de esto ya existe

`Admin/Index.razor` (`src/Quiniela.Web/Components/Pages/Admin/Index.razor:405-429`) **ya renderiza** `HomeSlotLabel`/`AwaySlotLabel` junto a cada selector:

```razor
<label class="form-label small text-muted mb-1">
    Local @(match.HomeSlotLabel is not null ? $"({match.HomeSlotLabel})" : "")
</label>
```

y `KnockoutService.AssignTeamsAsync` (ya implementado) permite asignar equipos a cualquier partido con `Stage != Grupos`. **No se requiere código nuevo para esto** — en cuanto C1 pueble `HomeSlotLabel`/`AwaySlotLabel` con el texto derivado de `nota` (ej. `"Ganador Partido 89"` en vez de vacío), el admin ya ve el origen del cruce automáticamente.

### Qué sí falta (ajuste menor, opcional)

- El dropdown de equipos (`allTeams`) sigue mostrando los 48 equipos sin filtrar. Se decidió **no** restringir por elegibilidad — fuera de alcance de C2 — así que no hay cambio ahí.
- Único ajuste real propuesto: una vez que el admin ya asignó el equipo real a un cruce, ocultar el `SlotLabel` original (ya es redundante) en vez de seguir mostrándolo junto al nombre del equipo:

```razor
@* Admin/Index.razor — ajuste opcional *@
<label class="form-label small text-muted mb-1">
    Local
    @if (match.HomeTeamId is null && match.HomeSlotLabel is not null)
    {
        <span>(@match.HomeSlotLabel)</span>
    }
</label>
```

### Archivos a modificar

| Archivo | Cambio |
|---|---|
| `Components/Pages/Admin/Index.razor` | (Opcional) ocultar `SlotLabel` una vez que el cruce ya tiene equipo asignado |

### Criterio de aceptación

- Con los datos de C1 cargados, el tab Bracket muestra junto a cada selector vacío el origen del cruce (ej. "Local (Ganador Partido 89)").
- No se modifica `KnockoutService.AssignTeamsAsync` ni el modelo de datos — este módulo es casi enteramente resultado de los datos de C1 + UI ya existente.

### Estimación: 0.5–1 hora (la mayor parte del trabajo ya estaba hecho en el Módulo 8)

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

Una predicción especial one-shot por sala: cada jugador elige al campeón del torneo. La ventana de captura/edición está atada a los partidos de 16avos y Octavos (no a una fecha fija configurable por el admin). Vale puntos fijos al acertar.

### Decisiones de producto

| Tema | Decisión |
|------|-------------------|
| ¿Cuándo se abre? | Cuando **todos** los partidos de 16avos (Dieciseisavos) están marcados como `Finalizado` |
| ¿Cuándo se cierra? | Al **inicio (kickoff) del primer partido de Octavos** — no es una fecha configurable manualmente, se calcula de los datos |
| Puntos por acierto | 10 pts fijos (configurable en `Pool`, campo `PtsChampion`, default 10) |
| ¿Se puede cambiar? | Sí, en cualquier momento **dentro** de la ventana (abierta → cierre) |
| Granularidad | Por sala (cada sala tiene su campeón, igual que pronósticos normales) |
| Recálculo | Automático al capturar el resultado de la Final |

**Nota:** se elimina el campo `ChampionDeadline` manual de la propuesta original — la ventana se deriva 100% del estado de los partidos (`MatchStage.Dieciseisavos` todos finalizados = apertura; `MIN(KickoffUtc)` de `MatchStage.Octavos` = cierre), igual que ya se calculan otros estados derivados en el proyecto (ej. `GetStagesWithMatchesAsync`).

### Ventana de tiempo y estados de la vista

La vista `/pools/{poolId}/champion` tiene **tres estados** según el momento en que se consulta:

**1. Antes de que abra (aún hay partidos de 16avos sin finalizar)**

- No se puede seleccionar ni guardar todavía (sin botón de guardar activo, o vista solo informativa).
- Se muestra igualmente la grilla de banderas, pero **filtrada** a los equipos que:
  - ya ganaron su partido de 16avos (pasaron a Octavos), **o**
  - todavía no juegan su partido de 16avos (siguen con vida, podrían pasar).
- Se excluyen de la grilla los equipos ya eliminados (perdieron un partido de 16avos ya finalizado).

**2. Ventana abierta (16avos 100% finalizados, antes del kickoff del primer Octavos)**

- Grilla con los 16 equipos clasificados a Octavos (ya no hace falta el filtro del estado 1, todos los 16avos están resueltos).
- Header grande: **"¿Qué selección consideras que ganará el mundial?"**
- Subtítulo: **"Si le atinas a quien gana el mundial puedes llevarte 10 puntos adicionales"**
- Debajo de la grilla, un botón para guardar el resultado.
- El jugador puede tocar una bandera para seleccionarla y cambiar de selección las veces que quiera mientras la ventana siga abierta.

**3. Ventana cerrada (ya inició el primer partido de Octavos)**

- Ya no se puede modificar la selección.
- Si el equipo elegido **sigue con vida**: se muestra solo su bandera (grande) con el texto **"Tu pronóstico es:"** seguido de la bandera.
- Si el equipo elegido **ya fue eliminado**: se muestra la misma bandera pero en **escala de grises**, con el texto **"JAJA no le atinaste"**.

### Grilla de banderas — layout responsivo

- **Desktop:** 4 banderas por fila (`grid-template-columns: repeat(4, 1fr)`).
- **Mobile:** 2 banderas por fila (`grid-template-columns: repeat(2, 1fr)`).
- Cada celda: bandera + nombre corto del equipo; celda seleccionada con borde/resaltado distintivo.

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
public int PtsChampion { get; set; } = 10;   // puntos por acertar al campeón
```

Migración: `AddChampionPrediction`

### Cálculo de la ventana (nuevo método en ChampionService)

```csharp
public enum ChampionWindowState { NotYetOpen, Open, Closed }

public async Task<ChampionWindowState> GetWindowStateAsync()
{
    var allDieciseisavosFinalized = await db.Matches
        .Where(m => m.Stage == MatchStage.Dieciseisavos)
        .AllAsync(m => m.Status == MatchStatus.Finalizado);

    if (!allDieciseisavosFinalized) return ChampionWindowState.NotYetOpen;

    var firstOctavosKickoff = await db.Matches
        .Where(m => m.Stage == MatchStage.Octavos)
        .OrderBy(m => m.KickoffUtc)
        .Select(m => (DateTime?)m.KickoffUtc)
        .FirstOrDefaultAsync();

    return firstOctavosKickoff is null || DateTime.UtcNow < firstOctavosKickoff
        ? ChampionWindowState.Open
        : ChampionWindowState.Closed;
}
```

Elegibilidad de equipos en el estado `NotYetOpen` (equipos ya eliminados en 16avos ya finalizados quedan fuera):

```csharp
public async Task<List<Team>> GetEligibleTeamsAsync()
{
    var dieciseisavos = await db.Matches
        .Where(m => m.Stage == MatchStage.Dieciseisavos)
        .ToListAsync();

    var eliminated = dieciseisavos
        .Where(m => m.Status == MatchStatus.Finalizado)
        .Select(m => m.HomeScore > m.AwayScore ? m.AwayTeamId : m.HomeTeamId)
        .ToHashSet();

    var allTeamIds = dieciseisavos.SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
        .Where(id => id != null).Select(id => id!.Value).ToHashSet();

    return await db.Teams.Where(t => allTeamIds.Contains(t.Id) && !eliminated.Contains(t.Id)).ToListAsync();
}
```

Eliminación de un equipo ya seleccionado (para el estado `Closed`, mostrar "JAJA no le atinaste"): un equipo está eliminado si perdió cualquier partido de fase eliminatoria ya finalizado (mismo criterio de `WinnerSide` que usa `BracketService`).

### Vista

`/pools/{poolId}/champion` — implementa los tres estados descritos arriba. Al guardar (solo en estado `Open`), confirma con un mensaje corto y deja el botón visible por si el jugador quiere cambiar su elección.

En Standings, una columna o fila extra "Pronóstico campeón" que muestra el equipo elegido por cada jugador (visible para todos en la sala).

### Integración con Scoring

Cuando se finaliza el partido de la Final, `ScoringService` llama a un método que compara `ChampionPrediction.TeamId` con el ganador de la Final y asigna `PtsChampion` a quien acertó.

### Integración con el Bracket (Módulo C) — banderas en escala de grises

Requisito transversal: en `/bracket`, las banderas de los equipos **eliminados** también deben mostrarse en escala de grises (hoy `BracketMatchCard.razor.css` solo atenúa el texto vía `.bmc-loser`, no la bandera). Agregar `filter: grayscale(1)` a `.bmc-loser .bmc-flag` en `Components/Shared/BracketMatchCard.razor.css`.

### Archivos a crear / modificar

| Archivo | Cambio |
|---|---|
| `Entities/ChampionPrediction.cs` | **Nueva entidad** |
| `Entities/Pool.cs` | + `PtsChampion` (sin `ChampionDeadline`, la ventana se calcula) |
| `Migrations/` | `AddChampionPrediction` |
| `Services/ChampionService.cs` | **Nuevo** — `GetWindowStateAsync`, `GetEligibleTeamsAsync`, upsert + recálculo tras la Final |
| `Components/Pages/Champion/Index.razor(.css)` | **Nueva página** — header/subtítulo, grilla de banderas (4 cols desktop / 2 cols mobile), 3 estados (NotYetOpen/Open/Closed), estado "JAJA no le atinaste" en gris |
| `Services/ScoringService.cs` | Recálculo tras partido de Final |
| `Program.cs` | Registrar `ChampionService` |
| `Components/Shared/BracketMatchCard.razor.css` | Agregar `filter: grayscale(1)` a la bandera del equipo perdedor/eliminado |

### Criterio de aceptación

- Con partidos de 16avos pendientes: no se puede guardar selección; la grilla solo muestra equipos ya clasificados a Octavos o que aún no juegan su 16avo.
- Con todos los 16avos finalizados y antes del kickoff del primer Octavos: el jugador puede elegir/cambiar su campeón libremente y guardar.
- Desde el kickoff del primer Octavos en adelante: la selección queda bloqueada (solo lectura).
- En modo solo-lectura, si el equipo elegido sigue con vida: se muestra "Tu pronóstico es:" + bandera a color.
- En modo solo-lectura, si el equipo elegido ya fue eliminado: se muestra "JAJA no le atinaste" + bandera en escala de grises.
- Grilla de banderas: 4 por fila en desktop, 2 por fila en mobile.
- Al finalizar la Final: se calculan los puntos y se suman al total de cada jugador.
- En `/bracket`, los equipos eliminados muestran su bandera en escala de grises.

### Estimación: 5–7 horas

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
| `Quiniela.Data/Seeding/Data/matches.json` | C1 |

---

## Orden de implementación sugerido

```
A (tabs dinámicos, 2h)
  └─> B (ver pronósticos por partido, 4h)
        └─> C (bracket visual, 8h)
              └─> C1 (seed Octavos→Final, 2-3h + datos)
                    └─> C2 (origen del cruce en admin, 0.5-1h)
                          └─> D (badge pendientes, 1h)
                                └─> E (stats personales, 6h)
                                      └─> F (historial posiciones, 5h)
                                            └─> H (predicción campeón, 5h)
```

Total estimado: **~34–48 horas** de trabajo.
