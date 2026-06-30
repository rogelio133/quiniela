# 05 — Mejoras: Bandera ondeante + TeamSheet enriquecido

**Fecha:** 2026-06-30  
**Estado:** Pendiente  

---

## Mejora 1 — Animación de bandera ondeante

### Contexto actual

Los módulos de **Pronosticar** y **Mis Pronósticos** muestran las banderas como círculos estáticos usando la clase `.mc-flag` definida en `MatchCard.razor.css`:

```css
.mc-flag {
    width: 48px !important;
    height: 48px !important;
    border-radius: 50%;          /* ← esto lo hace círculo */
    background-size: cover !important;
    border: 1.5px solid rgba(0,0,0,.08);
}
```

La bandera del header del `TeamSheet.razor` usa `.ts-flag` (mismo principio, también circular, 54×54px).  
Ambas clases pertenecen a la librería `flag-icons`, que renderiza la bandera como `background-image` sobre un `<span>`.

### Objetivo

Reemplazar el círculo estático por una bandera rectangular con animación CSS de ondeo (`wave`), de aspecto realista, sin dependencias externas.

### Diseño de la solución

#### Forma: de círculo a rectángulo con asta

- Cambiar `border-radius: 50%` → `border-radius: 4px` (borde sutil de bandera real).
- Proporciones: ancho `~1.5×` el alto (`72×48px` en match card, `81×54px` en team sheet).
- Agregar `overflow: hidden` para que la animación no desborde.

#### Animación keyframe de ondeo

El efecto de ondeo se logra animando `skewX` + ligera escala en X para simular la perspectiva de una tela moviéndose:

```css
@keyframes flagWave {
    0%   { transform: perspective(300px) rotateY(0deg)   skewX(0deg); }
    20%  { transform: perspective(300px) rotateY(6deg)   skewX(-2deg); }
    40%  { transform: perspective(300px) rotateY(-4deg)  skewX(1.5deg); }
    60%  { transform: perspective(300px) rotateY(5deg)   skewX(-1.5deg); }
    80%  { transform: perspective(300px) rotateY(-3deg)  skewX(1deg); }
    100% { transform: perspective(300px) rotateY(0deg)   skewX(0deg); }
}
```

- Duración: `2.8s` con `ease-in-out infinite` (suave, no agresivo).
- El `perspective(300px)` da sensación 3D sin distorsión excesiva en pantalla pequeña.

#### Sombra de profundidad

Añadir un sutil `box-shadow` lateral para reforzar el efecto de volumen:

```css
box-shadow: 2px 0 8px rgba(0,0,0,0.22), -1px 0 4px rgba(0,0,0,0.10);
```

### Archivos a modificar

| Archivo | Cambio |
|---|---|
| `wwwroot/css/animations.css` | Agregar `@keyframes flagWave` |
| `Components/Shared/MatchCard.razor.css` | Modificar `.mc-flag` (forma + clase `.mc-flag-wave`) |
| `Components/Shared/TeamSheet.razor.css` | Modificar `.ts-flag` (forma + animación) |
| `Components/Shared/MatchCard.razor` | Agregar clase `mc-flag-wave` al `<span class="fi ...">` |

> **Nota:** La animación debe estar en `animations.css` (donde ya viven `flagWave`, `shimmer`, etc.) para centralizar keyframes. Las clases de aplicación van en los CSS de componente con scoping de Blazor.

### Consideraciones

- En el modo **ReadOnly** (partidos finalizados), la bandera ya tiene clase `mc-flag-tappable` con `hover/active` de escala. La animación de ondeo debe coexistir; el `transform` de `:active` puede sobreescribirse usando `transform: scale(1.1)` directamente en el selector sin perder el wave.
- **Accesibilidad:** Agregar `@media (prefers-reduced-motion: reduce)` que elimine la animación y mantenga la forma rectangular estática.
- Los `<span>` con `fi fi-{code}` usan `background-image` — no SVG inline — por lo tanto la animación CSS sobre el elemento funciona sin restricciones.

---

## Mejora 2 — TeamSheet: datos de selección desde BD

### Contexto actual

El componente `TeamSheet.razor` muestra:
1. Header (bandera + nombre + grupo)
2. Posición en grupo (si aplica)
3. Partidos anteriores con pronóstico
4. Próximos partidos

Toda esta info viene de `TeamSheetService.cs` que consulta únicamente la entidad `Team` (campos: `Id`, `Name`, `FlagCode`, `ShortCode`, `GroupCode`).

El archivo `mundial_2026_2.json` en la raíz del repo contiene para **cada selección**:
- `dato_curioso` (string)
- `director_tecnico` (string)
- `jugadores[]` → `nombre`, `posicion`

Este dato **no está cargado en BD**.

### Objetivo

1. Extender el modelo de datos con los campos nuevos.
2. Cargar el JSON en BD durante el seed/migración.
3. Mostrar en `TeamSheet.razor` tres nuevas secciones:
   - **¿Sabías que?** — recuadro destacado con el dato curioso.
   - **Director Técnico** — nombre del DT.
   - **Jugadores convocados** — lista agrupada por posición (Portero · Defensa · Mediocampista · Delantero).
4. Garantizar que todo el panel sea scrolleable y visualmente atractivo con el estilo oscuro existente.

---

### Plan de implementación

#### Paso 1 — Entidades y migración EF Core

**`Quiniela.Data/Entities/Team.cs`** — agregar campos:

```csharp
public string? DatoCurioso      { get; set; }
public string? DirectorTecnico  { get; set; }

public ICollection<Jugador> Jugadores { get; set; } = [];
```

**Nueva entidad `Quiniela.Data/Entities/Jugador.cs`:**

```csharp
namespace Quiniela.Data.Entities;

public class Jugador
{
    public int    Id       { get; set; }
    public int    TeamId   { get; set; }
    public required string Nombre   { get; set; }
    public required string Posicion { get; set; }

    public Team Team { get; set; } = null!;
}
```

**`QuinielaDbContext.cs`** — agregar:

```csharp
public DbSet<Jugador> Jugadores => Set<Jugador>();
```

Y en `OnModelCreating`:

```csharp
modelBuilder.Entity<Jugador>(e => {
    e.HasOne(j => j.Team)
     .WithMany(t => t.Jugadores)
     .HasForeignKey(j => j.TeamId)
     .OnDelete(DeleteBehavior.Cascade);
});
```

Generar migración:

```bash
dotnet ef migrations add AddTeamInfoAndJugadores -p src/Quiniela.Data -s src/Quiniela.Web
```

---

#### Paso 2 — Seed desde `mundial_2026_2.json`

**`DbInitializer.cs`** — agregar método `SeedTeamInfoAsync`:

- Leer `mundial_2026_2.json` (ruta relativa al directorio de ejecución, o embeber como recurso).
- Deserializar con `System.Text.Json`.
- Para cada selección del JSON, buscar el `Team` en BD por `abreviacion` (que coincide con `ShortCode`) o por normalización del nombre.
- Si `DatoCurioso` o `DirectorTecnico` ya están llenos, omitir (idempotente).
- Si no hay jugadores para ese equipo, insertar.

**Tabla de coincidencia JSON → BD** (casos especiales a mapear):

| JSON `abreviacion` | BD `ShortCode` |
|---|---|
| `QAT` | `QAT` (nombre en BD: "Catar", JSON: "Qatar") |
| `RSA` | `RSA` |
| _resto_ | 1-a-1 |

El seed se invoca desde `Program.cs` (ya existe el patrón `DbInitializer.SeedAsync`).

---

#### Paso 3 — Actualizar `TeamSheetService.cs`

**`TeamSheetData`** — agregar campos:

```csharp
public string? DatoCurioso     { get; set; }
public string? DirectorTecnico { get; set; }
public List<Jugador> Jugadores { get; set; } = [];
public List<HistorialMundial> HistorialMundiales { get; set; } = [];
```

**`GetTeamSheetAsync`** — cambiar el `FindAsync` por una consulta que incluya jugadores:

```csharp
var team = await db.Teams
    .Include(t => t.Jugadores.OrderBy(j => j.Posicion).ThenBy(j => j.Nombre))
    .FirstOrDefaultAsync(t => t.Id == teamId)
    ?? throw new KeyNotFoundException($"Team {teamId} not found");
```

Y poblar el `TeamSheetData`:

```csharp
DatoCurioso    = team.DatoCurioso,
DirectorTecnico = team.DirectorTecnico,
Jugadores      = team.Jugadores.ToList(),
```

---

#### Paso 4 — Actualizar `TeamSheet.razor`

Agregar tres nuevas secciones entre el header y la posición de grupo:

##### Sección "¿Sabías que?"

```razor
@if (!string.IsNullOrWhiteSpace(data.DatoCurioso))
{
    <section class="ts-section ts-curiosity">
        <h6 class="ts-section-title">¿Sabías que?</h6>
        <div class="ts-curiosity-card">
            <span class="ts-curiosity-icon">💡</span>
            <p class="ts-curiosity-text">@data.DatoCurioso</p>
        </div>
    </section>
}
```

##### Sección "Historial mundialista" *(nuevo)*

Mostrar los últimos mundiales con badge coloreado según el resultado. Helper `HistorialClass` mapea la posición a una clase CSS.

```razor
@if (data.HistorialMundiales.Any())
{
    <section class="ts-section">
        <h6 class="ts-section-title">Historial mundialista</h6>
        <div class="ts-historial-list">
            @foreach (var h in data.HistorialMundiales)
            {
                <div class="ts-historial-row">
                    <span class="ts-historial-tournament">@h.Mundial</span>
                    <span class="ts-historial-badge ts-historial-@HistorialClass(h.Posicion)">@h.Posicion</span>
                </div>
            }
        </div>
    </section>
}
```

Paleta de badges:

| Clase | Color | Disparador |
|---|---|---|
| `ts-historial-champion` | Dorado | "campeón" |
| `ts-historial-final` | Plata | "final" (sin semi/cuartos/octavos) |
| `ts-historial-semi` | Violeta | "semifinal", "3er", "tercer" |
| `ts-historial-quarters` | Azul | "cuartos" |
| `ts-historial-round16` | Cian | "octavos", "segunda ronda" |
| `ts-historial-groups` | Gris claro | "grupos" |
| `ts-historial-none` | Gris tenue | resto (sin clasificar, etc.) |

##### Sección "Director Técnico"

```razor
@if (!string.IsNullOrWhiteSpace(data.DirectorTecnico))
{
    <section class="ts-section">
        <h6 class="ts-section-title">Director Técnico</h6>
        <div class="ts-dt-row">
            <span class="ts-dt-icon">🧑‍💼</span>
            <span class="ts-dt-name">@data.DirectorTecnico</span>
        </div>
    </section>
}
```

##### Sección "Jugadores convocados"

Agrupar por posición en orden: Portero → Defensa → Mediocampista → Delantero.

```razor
@if (data.Jugadores.Any())
{
    var posicionOrder = new[] { "Portero", "Defensa", "Mediocampista", "Delantero" };
    var grupos = data.Jugadores
        .GroupBy(j => j.Posicion)
        .OrderBy(g => Array.IndexOf(posicionOrder, g.Key));

    <section class="ts-section ts-section-last">
        <h6 class="ts-section-title">Jugadores convocados</h6>
        @foreach (var grupo in grupos)
        {
            <p class="ts-pos-label">@grupo.Key</p>
            <div class="ts-players-grid">
                @foreach (var j in grupo)
                {
                    <div class="ts-player-chip">@j.Nombre</div>
                }
            </div>
        }
    </section>
}
```

---

#### Paso 5 — Estilos `TeamSheet.razor.css`

Agregar al archivo CSS del componente:

```css
/* ── Dato curioso ─────────────────────────────────────────── */
.ts-curiosity-card {
    display: flex;
    gap: 10px;
    align-items: flex-start;
    background: linear-gradient(135deg, rgba(245,158,11,0.10) 0%, rgba(217,119,6,0.06) 100%);
    border: 1px solid rgba(245,158,11,0.22);
    border-radius: 14px;
    padding: 14px 16px;
}

.ts-curiosity-icon {
    font-size: 1.2rem;
    flex-shrink: 0;
    line-height: 1.4;
}

.ts-curiosity-text {
    font-size: 0.78rem;
    color: rgba(255,255,255,0.75);
    line-height: 1.55;
    margin: 0;
}

/* ── Director técnico ─────────────────────────────────────── */
.ts-dt-row {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 10px 14px;
    background: rgba(255,255,255,0.04);
    border-radius: 12px;
    border: 1px solid rgba(255,255,255,0.07);
}

.ts-dt-icon { font-size: 1rem; }

.ts-dt-name {
    font-size: 0.88rem;
    font-weight: 600;
    color: rgba(255,255,255,0.88);
}

/* ── Jugadores ────────────────────────────────────────────── */
.ts-pos-label {
    font-size: 0.56rem;
    font-weight: 700;
    letter-spacing: 0.1em;
    text-transform: uppercase;
    color: rgba(139,92,246,0.75);
    margin: 12px 0 6px;
}

.ts-pos-label:first-of-type { margin-top: 0; }

.ts-players-grid {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    margin-bottom: 4px;
}

.ts-player-chip {
    font-size: 0.7rem;
    font-weight: 500;
    color: rgba(255,255,255,0.80);
    background: rgba(255,255,255,0.06);
    border: 1px solid rgba(255,255,255,0.10);
    border-radius: 20px;
    padding: 5px 12px;
    white-space: nowrap;
    transition: background 0.12s;
}

.ts-player-chip:hover {
    background: rgba(99,102,241,0.18);
    border-color: rgba(99,102,241,0.35);
}
```

---

### Scroll y altura del panel

El `.ts-panel` ya tiene `max-height: 82dvh` y `overflow-y: auto`. Con las nuevas secciones el panel crecerá, activando el scroll automáticamente. Se debe verificar que en iPhone SE (375px) el panel no oculte el handle y el header — están `position: sticky` implícito por estar al inicio. Si se detecta que el header queda fuera del scroll, se agrega:

```css
.ts-header {
    position: sticky;
    top: 0;
    z-index: 1;
}
```

---

### Orden final de secciones en TeamSheet

1. Handle (drag indicator)
2. Header (bandera ondeante · nombre · grupo · botón cerrar) — **sticky top**
3. ¿Sabías que? *(nuevo)*
4. Historial mundialista *(nuevo)*
5. Director Técnico *(nuevo)*
6. Posición en grupo *(existente)*
7. Partidos anteriores *(existente)*
8. Próximos partidos *(existente)*
9. Jugadores convocados *(nuevo)* — al final, es la sección más larga

---

## Resumen de archivos afectados

| Archivo | Acción | Mejora |
|---|---|---|
| `wwwroot/css/animations.css` | Agregar `@keyframes flagWave` | 1 |
| `Components/Shared/MatchCard.razor.css` | `.mc-flag` → rectangular + wave | 1 |
| `Components/Shared/TeamSheet.razor.css` | `.ts-flag` + estilos nuevos | 1 + 2 |
| `Components/Shared/MatchCard.razor` | Agregar clase `mc-flag-wave` al span | 1 |
| `Components/Shared/TeamSheet.razor` | Secciones nuevas + orden | 2 |
| `Quiniela.Data/Entities/Team.cs` | Campos `DatoCurioso`, `DirectorTecnico`, nav `Jugadores`, nav `HistorialMundiales` | 2 |
| `Quiniela.Data/Entities/Jugador.cs` | Nueva entidad | 2 |
| `Quiniela.Data/Entities/HistorialMundial.cs` | Nueva entidad (`Mundial`, `Posicion`) | 2 |
| `Quiniela.Data/QuinielaDbContext.cs` | `DbSet<Jugador>` + `DbSet<HistorialMundial>` + config | 2 |
| `Quiniela.Data/Migrations/` | Migraciones EF (`AddTeamInfoAndJugadores`, `AddHistorialMundial`) | 2 |
| `Quiniela.Data/Seeding/DbInitializer.cs` | `SeedTeamInfoAsync` + `SeedHistorialAsync` desde JSON | 2 |
| `Quiniela.Web/Services/TeamSheetService.cs` | Include jugadores + historial + nuevos campos en DTO | 2 |
