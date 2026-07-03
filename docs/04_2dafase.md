# Plan de Desarrollo — Segunda Fase (Eliminatorias)
## Archivo: 04_2dafase.md

> Mundial 2026 · Directrices para habilitar las fases de eliminación directa,
> la predicción de instancia con puntaje extra, y la reorganización del menú
> **Grupos → Fases**.

---

## 0. Contexto y decisiones tomadas

Resumen de las decisiones de producto acordadas (sirven como fuente de verdad
para todo lo que sigue):

| Tema | Decisión |
|------|----------|
| **Resultado en eliminatorias** | Se **elimina el botón "Empate"**. Solo se predice **quién avanza** (Local / Visitante) → **3 pts**. |
| **Instancia del partido** | Nuevo selector **90 min · Tiempo extra · Penales** → **+2 pts** al atinarle. |
| **Independencia del bonus** | El **+2 de instancia es independiente** del acierto de avance. Se puede ganar el +2 aunque se falle quién avanza, y viceversa. |
| **Máximo por partido (KO)** | **5 pts** (3 avance + 2 instancia). |
| **Grupos** | **Sin cambios**: sigue con Local/Empate/Visitante = 3 pts, sin instancia. |
| **Captura de marcador** | El predictor **no captura marcador**. Solo el admin captura el **marcador global manual** (incluyendo goles de penales) + la **instancia alcanzada**. |
| **Carga de partidos de Dieciseisavos** | **Desde archivo JSON local** (`mundial2026_dieciseisavos.json`): se lee y siembra en `DbInitializer`, igual que los partidos de grupos. Sin red ni API. El armado manual del admin queda como respaldo/corrección. |
| **Menú** | "Grupos" pasa a llamarse **"Fases"** con tabs: **Grupos** (actual) + **Dieciseisavos**. |

### Lo que la base de datos YA soporta (no rehacer)

- `MatchStage` (`src/Quiniela.Data/Entities/Match.cs`) **ya contempla** todas las
  fases: `Grupos, Dieciseisavos, Octavos, Cuartos, Semifinal, TercerLugar, Final`.
  No se requiere tocar el enum.
- `Match.HomeTeamId` / `AwayTeamId` son **nullable** → permiten partidos
  "placeholder" sin equipos definidos.
- `Pool.PtsCorrect = 3` y `Pool.PtsBonusKO = 2` ya existen y son configurables por sala.
- `ScoringService` **ya otorga +2 automáticos** a cualquier acierto de eliminatoria
  (`ScoringService.cs:19,28-30`). ⚠️ **Esta semántica cambia** (ver Módulo 2).

---

## Módulo A — Base de datos: equipos a Dieciseisavos y soporte de fases

### A.1 Cambios de esquema (migración EF Core)

Se necesitan columnas nuevas. Generar **una sola migración** (`Add2ndPhaseSupport`)
con lo siguiente:

**`Match` — nuevas columnas:**

```csharp
// src/Quiniela.Data/Entities/Match.cs
public enum MatchDecidedIn
{
    Regular90  = 0,   // se definió en tiempo regular (90')
    ExtraTime  = 1,   // se definió en tiempo extra (120')
    Penalties  = 2    // se definió en penales
}

public class Match
{
    // ...campos actuales...

    // Instancia a la que llegó el partido. NULL en grupos y en KO no finalizados.
    public MatchDecidedIn? DecidedIn { get; set; }

    // Etiquetas del cruce cuando aún no se conocen los equipos (bracket placeholder).
    // Ej: "1A", "2B", "3° C/D/F/G". Se muestran mientras HomeTeamId/AwayTeamId son null.
    public string? HomeSlotLabel { get; set; }   // nvarchar(20)
    public string? AwaySlotLabel { get; set; }   // nvarchar(20)

    // Orden del partido dentro de su fase (1..16 en dieciseisavos), para el bracket.
    public int? BracketOrder { get; set; }
}
```

**`Prediction` — nueva columna:**

```csharp
// src/Quiniela.Data/Entities/Prediction.cs
public class Prediction
{
    // ...campos actuales...

    // Instancia pronosticada por el jugador. NULL en grupos. Reusa MatchDecidedIn.
    public MatchDecidedIn? PredInstance { get; set; }

    // (Opcional recomendado) Desglose de puntos para Tabla de Posiciones — ver Módulo 3.
    // Si no se agrega, Points sigue siendo el total. Ver nota en Módulo 3 sobre conteo de aciertos.
}
```

> **Nota sobre `PredOutcome`:** se mantiene `char(1)` con `'H'`/`'A'` en KO
> (sin `'D'`). No cambia el tipo de columna. En grupos sigue aceptando `'H'/'D'/'A'`.

**Configuración en `QuinielaDbContext.OnModelCreating`:**

```csharp
modelBuilder.Entity<Match>(e =>
{
    // ...config actual...
    e.Property(m => m.HomeSlotLabel).HasMaxLength(20);
    e.Property(m => m.AwaySlotLabel).HasMaxLength(20);
});
```

Los `enum` se persisten como `int` por convención EF — no requieren conversión extra.

> Comando: `dotnet ef migrations add Add2ndPhaseSupport --project src/Quiniela.Data --startup-project src/Quiniela.Web`
> Aplicar con `dotnet ef database update` (o se aplica al arrancar si el proyecto usa `MigrateAsync`).

### A.2 Carga de los 16 partidos de Dieciseisavos (Round of 32) — desde archivo JSON local

Formato: 12 grupos × 2 (1° y 2°) + 8 mejores terceros = **32 selecciones → 16 partidos**.

**Fuente de datos: el archivo `mundial2026_dieciseisavos.json`** (ya en la raíz del
repo). Contiene los 16 partidos reales con equipos ya definidos. Sin red, sin API,
sin preview/commit: se lee el archivo y se siembra directo en `DbInitializer`, igual
que los partidos de grupos. Estructura del JSON:

```json
{
  "torneo": "FIFA World Cup 2026",
  "fase": "Dieciseisavos de final (Round of 32)",
  "partidos": [
    { "match_id": 73, "fecha_utc": "2026-06-28T19:00:00Z",
      "local": "Sudáfrica", "visita": "Canadá",
      "venue": "SoFi Stadium, Los Ángeles, EE.UU." },
    // … 16 partidos …
  ]
}
```

#### 1) Ubicación del archivo (recurso embebido)

Copiar el JSON a `src/Quiniela.Data/Seeding/Data/mundial2026_dieciseisavos.json` y
marcarlo como **recurso embebido** para que viaje con el ensamblado (no depende de
rutas de disco en runtime). En `Quiniela.Data.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Seeding\Data\mundial2026_dieciseisavos.json" />
</ItemGroup>
```

#### 2) Sembrado en `DbInitializer`

Agregar `SeedDieciseisavosAsync`, llamado desde `SeedAsync` después de grupos.
Reusa el mapeo de equipos por nombre que ya existe en `SeedTeamsAndMatchesAsync`.

```csharp
// DTOs del JSON
private record KnockoutFile(List<KnockoutMatchJson> Partidos);
private record KnockoutMatchJson(int Match_Id, DateTime Fecha_Utc, string Local, string Visita, string Venue);

// Nombres del JSON que difieren de los sembrados en BD (normalización mínima)
private static readonly Dictionary<string, string> NameFix = new(StringComparer.OrdinalIgnoreCase)
{
    ["R.D. del Congo"] = "Congo RD",   // ⚠️ el JSON usa otra grafía que el seed de equipos
};

private static async Task SeedDieciseisavosAsync(QuinielaDbContext context, ILogger logger)
{
    // Idempotente: si ya hay partidos de esta fase, no hacer nada.
    if (await context.Matches.AnyAsync(m => m.Stage == MatchStage.Dieciseisavos))
        return;

    // Leer el JSON embebido
    var assembly = typeof(DbInitializer).Assembly;
    await using var stream = assembly.GetManifestResourceStream(
        "Quiniela.Data.Seeding.Data.mundial2026_dieciseisavos.json")
        ?? throw new InvalidOperationException("No se encontró el JSON de dieciseisavos embebido.");

    var file = await JsonSerializer.DeserializeAsync<KnockoutFile>(stream,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("JSON de dieciseisavos inválido o vacío.");

    var teamsByName = await context.Teams.ToDictionaryAsync(t => t.Name, StringComparer.OrdinalIgnoreCase);

    int Resolve(string name)
    {
        var key = NameFix.GetValueOrDefault(name, name);
        return teamsByName.TryGetValue(key, out var t)
            ? t.Id
            : throw new InvalidOperationException($"Equipo de dieciseisavos no encontrado en BD: '{name}'.");
    }

    var matches = file.Partidos
        .OrderBy(p => p.Fecha_Utc)
        .Select((p, i) => new Match
        {
            HomeTeamId = Resolve(p.Local),
            AwayTeamId = Resolve(p.Visita),
            KickoffUtc = DateTime.SpecifyKind(p.Fecha_Utc, DateTimeKind.Utc),
            Venue      = p.Venue.Split(',')[0].Trim(),  // "SoFi Stadium, …" → "SoFi Stadium"
            Stage      = MatchStage.Dieciseisavos,
            BracketOrder = i + 1,
            Status     = MatchStatus.Programado,
        })
        .ToList();

    context.Matches.AddRange(matches);
    await context.SaveChangesAsync();
    logger.LogInformation("Seeded {Count} round-of-32 matches from JSON.", matches.Count);
}
```

> **Notas:**
> - El JSON usa **`"R.D. del Congo"`** mientras el seed de equipos tiene **`"Congo RD"`**
>   (`DbInitializer.cs:79`). El diccionario `NameFix` resuelve esa única discrepancia;
>   los otros 31 nombres coinciden exactamente. Si el mapeo falla, se lanza excepción
>   visible (no se siembra a medias).
> - El `Venue` del JSON es largo ("SoFi Stadium, Los Ángeles, EE.UU."); se recorta al
>   nombre del estadio para mantener consistencia visual con los partidos de grupos
>   (que usan "Estadio Azteca", etc.). `Match.Venue` es `nvarchar(100)`.
> - `BracketOrder` se asigna por orden cronológico (1..16).
> - El selector de instancia/avance (Módulo B) funciona porque los equipos ya están
>   definidos (`PredictionService` filtra `HomeTeamId != null && AwayTeamId != null`).

Para **fases siguientes** (octavos→final) se mantienen `HomeSlotLabel`/`AwaySlotLabel`
(Módulo A.1) como placeholders hasta que se definan esos cruces (ver A.3); cuando se
tengan, se puede repetir el patrón con un JSON análogo por fase.

### A.3 Preparar para fases siguientes (Octavos → Final)

El esquema con `Stage`, `SlotLabel`, `BracketOrder` y equipos nullable **ya soporta
todas las fases**. Para octavos/cuartos/semis/final se replica el mismo patrón de
sembrado de placeholders. **Recomendación:** sembrar la estructura completa del
bracket (16+8+4+2+1+1 = 32 partidos KO) de una vez con sus `SlotLabel`, así el
admin solo va asignando equipos conforme avanzan las rondas. Esto deja el árbol
listo end-to-end sin nuevas migraciones por fase.

### A.4 Asignación/corrección de equipos (servicio de respaldo)

La carga principal de dieciseisavos viene del JSON local (A.2). Este servicio es el
**respaldo manual** para corregir datos erróneos/faltantes y para asignar equipos en
las **fases siguientes** (octavos→final), que se siembran como placeholders. Crear
`KnockoutService` con:

- `Task AssignTeamsAsync(int matchId, int? homeTeamId, int? awayTeamId)` — el admin
  asigna/corrige y persiste los equipos en el `Match`.
- Validar que un equipo no quede asignado a dos partidos de la misma ronda.

El panel de admin (Módulo C.2) consume este servicio para editar partidos.

---

## Módulo B — Predicciones: avance + instancia con puntaje extra

### B.1 Entidad y servicio

- `Prediction.PredInstance` (ver A.1) guarda la instancia pronosticada.
- `PredictionService.UpsertAsync(...)` debe aceptar la instancia. Nueva firma:

```csharp
public async Task<(bool Success, string? Error)> UpsertAsync(
    int userId, int poolId, int matchId, char outcome, MatchDecidedIn? instance)
{
    var match = await db.Matches.FindAsync(matchId);
    bool isKnockout = match is not null && match.Stage != MatchStage.Grupos;

    // En grupos: outcome ∈ {H,D,A}, instance debe ser null.
    // En KO:     outcome ∈ {H,A} (sin empate), instance es obligatoria.
    if (isKnockout)
    {
        if (outcome is not ('H' or 'A'))
            return (false, "En eliminatorias debes elegir quién avanza.");
        if (instance is null)
            return (false, "Selecciona la instancia (90 min, tiempo extra o penales).");
    }
    else if (outcome is not ('H' or 'D' or 'A'))
        return (false, "Resultado inválido.");

    // …resto del upsert (igual que hoy), guardando PredInstance = isKnockout ? instance : null
}
```

> **Validación de cierre:** se mantiene la regla actual — no se permite modificar
> tras `KickoffUtc <= UtcNow` (`PredictionService.cs:51`).

### B.2 Motor de puntaje (`ScoringService`) — ⚠️ cambio de semántica

Hoy `RecalculateForMatchAsync` da `PtsCorrect + PtsBonusKO` a cualquier acierto de
KO (`ScoringService.cs:28-30`). Bajo la nueva regla, **`PtsBonusKO` deja de ser
automático** y pasa a ser el **bonus de instancia**, que se evalúa por separado:

```csharp
public async Task RecalculateForMatchAsync(int matchId)
{
    var match = await db.Matches.FindAsync(matchId);
    if (match is null || match.HomeScore is null || match.AwayScore is null) return;

    bool isKnockout = match.Stage != MatchStage.Grupos;

    // En KO no hay empate: el marcador global (incl. penales) define el avance.
    char realOutcome = match.HomeScore > match.AwayScore ? 'H'
                     : match.HomeScore < match.AwayScore ? 'A'
                     : 'D';   // 'D' solo posible en grupos

    var predictions = await db.Predictions
        .Where(p => p.MatchId == matchId).Include(p => p.Pool).ToListAsync();

    foreach (var pred in predictions)
    {
        int pts = 0;

        // (1) Acierto de avance/resultado → PtsCorrect (3)
        if (pred.PredOutcome == realOutcome)
            pts += pred.Pool.PtsCorrect;

        // (2) Acierto de instancia → PtsBonusKO (2), INDEPENDIENTE del avance, solo en KO
        if (isKnockout && match.DecidedIn is not null && pred.PredInstance == match.DecidedIn)
            pts += pred.Pool.PtsBonusKO;

        pred.Points = pts;
    }

    await db.SaveChangesAsync();
}
```

**Puntos:** grupos → 0 o 3. KO → 0, 2, 3 o 5 (avance y/o instancia, independientes).

### B.3 UI — `MatchCard.razor` (respetar el diseño actual)

La tarjeta ya tiene tres columnas (`mc-option`): Local / `mc-draw-col` Empate / Visitante.
Cambios manteniendo el lenguaje visual existente (clases `mc-*`, scoped en
`MatchCard.razor.css`):

1. **Ocultar Empate en KO.** Renderizar `mc-draw-col` solo cuando
   `Match.Stage == MatchStage.Grupos`. En KO quedan 2 columnas (ajustar grid a 2
   en lugar de 3 vía clase contextual, p. ej. `mc-options-ko`).

2. **Nuevo selector de instancia** (solo KO, debajo de `mc-options`, antes del footer):
   tres "chips" seleccionables consistentes con `mc-option-selected`.

```razor
@if (Match.Stage != MatchStage.Grupos)
{
    <div class="mc-instance">
        <span class="mc-instance-label">¿Cómo se define?</span>
        <div class="mc-instance-opts">
            <button class="mc-inst-chip @(selectedInstance == MatchDecidedIn.Regular90 ? "mc-inst-selected" : "")"
                    @onclick="() => SelectInstance(MatchDecidedIn.Regular90)" disabled="@(!IsInteractive)">90'</button>
            <button class="mc-inst-chip @(selectedInstance == MatchDecidedIn.ExtraTime ? "mc-inst-selected" : "")"
                    @onclick="() => SelectInstance(MatchDecidedIn.ExtraTime)" disabled="@(!IsInteractive)">T. Extra</button>
            <button class="mc-inst-chip @(selectedInstance == MatchDecidedIn.Penalties ? "mc-inst-selected" : "")"
                    @onclick="() => SelectInstance(MatchDecidedIn.Penalties)" disabled="@(!IsInteractive)">Penales</button>
        </div>
    </div>
}
```

3. **`Save`** debe enviar `selectedInstance` y exigirla en KO (mensaje:
   "Selecciona la instancia antes de guardar").

4. **Footer de resultado (`mc-result-footer`)**: además del marcador, mostrar la
   instancia real (`90' / T.Extra / Penales`) y un desglose del puntaje, p. ej.:
   `+3 avance ✓  ·  +2 instancia ✓` o los aciertos parciales que correspondan.

**CSS sugerido** (`MatchCard.razor.css`, alineado a la paleta `#1A56DB / #059669 / #F59E0B`):

```css
.mc-instance { padding: 8px 12px; border-top: 1px dashed rgba(0,0,0,.08); }
.mc-instance-label { font-size: .62rem; font-weight: 700; letter-spacing: .06em;
    color: #64748B; text-transform: uppercase; display: block; margin-bottom: 6px; }
.mc-instance-opts { display: flex; gap: 6px; }
.mc-inst-chip { flex: 1; padding: 7px 4px; border: 1.5px solid #E2E8F0; border-radius: 8px;
    background: #fff; font-size: .72rem; font-weight: 600; color: #475569; cursor: pointer; }
.mc-inst-chip:disabled { opacity: .55; cursor: default; }
.mc-inst-selected { border-color: #1A56DB; background: #EFF4FF; color: #1A56DB; }

/* En KO, dos columnas en lugar de tres */
.mc-options-ko { grid-template-columns: 1fr 1fr; }
```

### B.4 `MyPredictions.razor`

Reusa `MatchCard` en modo `ReadOnly`. Verificar que el selector de instancia se
muestre deshabilitado mostrando lo pronosticado, y que el footer muestre el
desglose de puntos (avance / instancia).

---

## Módulo C — Panel Admin: captura de instancia y armado del bracket

### C.1 Captura de resultado con instancia

`AdminService.SaveResultAsync` debe recibir la instancia en KO:

```csharp
public async Task<(bool Success, string? Error)> SaveResultAsync(
    int matchId, int homeScore, int awayScore, MatchDecidedIn? decidedIn)
{
    // …validaciones actuales…
    var match = await db.Matches.FindAsync(matchId);
    bool isKnockout = match!.Stage != MatchStage.Grupos;

    if (isKnockout)
    {
        if (decidedIn is null)
            return (false, "Indica la instancia (90', tiempo extra o penales).");
        if (homeScore == awayScore)
            return (false, "En eliminatorias el marcador global no puede ser empate (incluye penales).");
    }

    match.HomeScore = homeScore;
    match.AwayScore = awayScore;
    match.DecidedIn = isKnockout ? decidedIn : null;
    match.Status = MatchStatus.Finalizado;
    await db.SaveChangesAsync();
    await scoringService.RecalculateForMatchAsync(matchId);
    return (true, null);
}
```

En `Admin/Index.razor`, en la fila de captura de un partido de KO, agregar un
selector de instancia (mismo patrón visual de chips) junto a los inputs de goles.
Aclarar en el label que el marcador es **global incluyendo penales**.

### C.2 Sección de armado del bracket (corrección manual)

Dieciseisavos se carga automáticamente desde el JSON (A.2). Esta vista sirve para
**corregir** ese cargado si hiciera falta y para **asignar equipos en fases
siguientes** (octavos→final), que se siembran como placeholders. Agregar en el panel
admin una vista que:

- Liste los partidos de KO con equipos `null` (placeholders, mostrando `SlotLabel`).
- Permita **asignar o corregir** los equipos (`KnockoutService.AssignTeamsAsync`).

> El "publicar" es implícito: en cuanto un partido tiene ambos equipos asignados,
> ya aparece en Predicciones (que filtra `HomeTeamId != null && AwayTeamId != null`,
> `PredictionService.cs:23`). Verificar este filtro: hoy oculta correctamente los
> placeholders sin equipos. ✔️

---

## Módulo D — Menú "Grupos" → "Fases" con tabs

### D.1 Navegación

- `NavMenu.razor:28-30`: cambiar texto **"Grupos" → "Fases"** y `href="grupos"` →
  `href="fases"` (mantener el ícono `bi-table-nav-menu`).
- Renombrar la página `Components/Pages/Groups/Index.razor`:
  - `@page "/fases"` (agregar `@page "/grupos"` como alias para no romper enlaces existentes).
  - `<PageTitle>` y encabezado → "Fases".

### D.2 Estructura de tabs

Usar el **mismo patrón de tabs Bootstrap que ya existe en el panel admin**
(`Admin/Index.razor:54-60`, clases `nav nav-tabs`) para mantener consistencia:

```razor
<ul class="nav nav-tabs mb-3" role="tablist">
    <li class="nav-item">
        <button class="nav-link @(activeTab == FaseTab.Grupos ? "active" : "")"
                @onclick="() => activeTab = FaseTab.Grupos" type="button">Grupos</button>
    </li>
    <li class="nav-item">
        <button class="nav-link @(activeTab == FaseTab.Dieciseisavos ? "active" : "")"
                @onclick="() => activeTab = FaseTab.Dieciseisavos" type="button">Dieciseisavos</button>
    </li>
</ul>

@if (activeTab == FaseTab.Grupos)
{
    @* …todo el contenido actual de la página de grupos (acordeones) … *@
}
else
{
    @* Tab Dieciseisavos: lista de los 16 partidos de la ronda *@
}
```

- **Tab Grupos:** mover sin cambios el contenido actual (acordeones por grupo).
- **Tab Dieciseisavos:** listar los 16 partidos de la ronda. Reusar `MatchCard`
  en modo `ReadOnly` (informativo, sin pronóstico) **o** una tarjeta de bracket más
  ligera. Mostrar `SlotLabel` cuando los equipos aún no estén definidos
  (ej. *"1A vs 3° C/D/F"*) y banderas+nombre cuando ya estén asignados.
- **Extensible:** dejar el `enum FaseTab` y el switch listos para sumar tabs
  *Octavos, Cuartos, Semifinal, Final* conforme avance el torneo.

> Crear servicio/método `GetMatchesByStageAsync(MatchStage stage)` (o ampliar
> `PredictionService`/`KnockoutService`) para alimentar el tab sin lógica en la vista.

---

## Módulo E — Tabla de Posiciones y puntos finos (lo que se podría pasar)

Esto es lo que conviene revisar para que la nueva dinámica no rompa nada
existente — especialmente **Tabla de Posiciones**:

1. **Conteo de "aciertos" (`StandingsService.cs:36`).** Hoy cuenta
   `CorrectPredictions = Count(p => p.Points > 0)`. Con el bonus de instancia
   **independiente**, una predicción con `Points == 2` (solo instancia, avance
   fallado) contaría como "acierto" aunque el jugador erró quién avanza.
   **Decidir:** ¿qué significa "acierto" en la tabla?
   - Opción simple: dejarlo como `Points > 0` (cualquier punto = acierto).
   - Opción precisa: contar solo aciertos de **resultado/avance**. Para esto
     conviene un desglose de puntos (`PtsResult` / `PtsInstance` en `Prediction`,
     mencionado en A.1) o recalcular el outcome al vuelo.
   - **Recomendación:** guardar el desglose en `Prediction` (2 columnas int) para
     poder mostrar en la tabla "X aciertos de resultado · Y de instancia" sin
     ambigüedad.

2. **`Points` máximo por partido cambió** (3 en grupos, 5 en KO). La barra de
   progreso de `Standings/Index.razor` es **relativa al líder**, así que no se
   rompe; pero si en algún lado se muestra un "% sobre máximo posible", recalcular
   considerando que los partidos KO valen 5.

3. **Recálculo retroactivo.** Si ya hubiera partidos de KO finalizados con la
   semántica vieja (improbable hoy, junio 2026, antes de eliminatorias), correr
   `RecalculateForMatchAsync` para todos los KO finalizados tras desplegar el
   cambio de `ScoringService`.

4. **`GetFinalizedMatchCountAsync`** (contexto de la tabla) no cambia, pero el
   contador mezclará grupos + KO; está bien, solo confirmar copy.

5. **Empate en KO imposible:** validar en captura (C.1) que el marcador global de
   KO nunca sea empate (siempre hay avance al incluir penales).

6. **Zona horaria / cierre de pronóstico:** los partidos de KO heredan la misma
   lógica de `KickoffUtc` y cierre; no requiere cambios.

---

## Resumen de archivos a tocar

| Archivo | Cambio |
|---------|--------|
| `Entities/Match.cs` | + `MatchDecidedIn` enum, `DecidedIn`, `HomeSlotLabel`, `AwaySlotLabel`, `BracketOrder` |
| `Entities/Prediction.cs` | + `PredInstance` (+ opcional desglose de puntos) |
| `QuinielaDbContext.cs` | config de longitudes de los nuevos `SlotLabel` |
| `Migrations/` | nueva migración `Add2ndPhaseSupport` |
| `Seeding/Data/mundial2026_dieciseisavos.json` | **nuevo** — JSON de los 16 partidos (copia embebida del archivo de la raíz) |
| `Quiniela.Data.csproj` | marcar el JSON como `<EmbeddedResource>` |
| `Seeding/DbInitializer.cs` | `SeedDieciseisavosAsync` (lee el JSON embebido) + placeholders de fases siguientes |
| `Services/ScoringService.cs` | nueva semántica avance + instancia independiente |
| `Services/PredictionService.cs` | `UpsertAsync` con instancia + validación KO sin empate |
| `Services/AdminService.cs` | `SaveResultAsync` con instancia |
| `Services/KnockoutService.cs` | **nuevo** — asignación/corrección manual (respaldo) |
| `Services/StandingsService.cs` | revisar conteo de aciertos (Módulo E.1) |
| `Shared/MatchCard.razor(.css)` | ocultar empate en KO + selector de instancia + footer desglose |
| `Pages/Admin/Index.razor` | selector de instancia en captura + sección bracket |
| `Pages/Groups/Index.razor` | → `/fases`, tabs Grupos/Dieciseisavos |
| `Layout/NavMenu.razor` | "Grupos" → "Fases", `href="fases"` |

---

## Orden de implementación sugerido

1. [x] **Esquema + migración** (Módulo A.1) — base para todo lo demás.
2. [x] **Carga de dieciseisavos desde JSON** (A.2) — `SeedDieciseisavosAsync`; placeholders de fases siguientes (A.3) diferido (sin fechas/sedes reales aún para Octavos→Final).
3. [x] **ScoringService + PredictionService + AdminService** (B.2, B.1, C.1) — lógica de puntos. Se agregó desglose `PtsResult`/`PtsInstance` en `Prediction` (migración `AddPredictionPointsBreakdown`, aplicada) para soportar el conteo preciso de aciertos del Módulo E.
4. [x] **MatchCard** (B.3) — UI de predicción. Empate oculto en KO, selector de instancia (90'/T.Extra/Penales), footer con desglose de puntos (avance ✓/✗, instancia ✓/✗).
5. [x] **KnockoutService + panel admin** (A.4, C.2) — `KnockoutService.AssignTeamsAsync` (con validación de equipo duplicado por ronda) + tab "Bracket" en `Admin/Index.razor` para asignar/corregir equipos de partidos placeholder. Selector de instancia agregado a la captura de resultados KO (pendientes y corrección de finalizados).
6. [x] **Menú Fases con tabs** (Módulo D). `NavMenu` → "Fases" (`/fases`), `Groups/Index.razor` con tabs Grupos/Dieciseisavos (`/grupos` se mantiene como alias). Tab Dieciseisavos lista los 16 partidos vía `KnockoutService.GetMatchesByStageAsync`.
7. [x] **Ajustes de Tabla de Posiciones** (Módulo E). Se eligió la opción precisa: `StandingsService.CorrectPredictions` ahora cuenta `PtsResult > 0` (solo aciertos de avance/resultado), no `Points > 0`.
