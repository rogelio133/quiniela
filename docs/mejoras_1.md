# Mejoras 1 — Historial, comparativas y gamificación

**Fecha:** 2026-07-04
**Estado:** Pendiente
**Contexto:** Módulos 0–8 y A–H (`docs/06_mejorasfases.md`) completos, torneo en fase de eliminatorias. Este documento recoge un segundo lote de mejoras, centradas en explotar datos que el proyecto ya recolecta (`StandingsSnapshot`, desglose `PtsResult`/`PtsInstance`, `ChampionPrediction`) para dar más profundidad social/competitiva a la app, sin tocar el flujo core de captura de resultados.

---

## Resumen de prioridades

| # | Módulo | Urgencia | Esfuerzo | Impacto | Estado |
|---|--------|----------|----------|---------|--------|
| I | Gráfica de evolución de posición | 🟡 Media | ~5–6 h | Medio | ✅ Finalizado |
| J | Head-to-head entre dos jugadores | 🟢 Baja | ~5–7 h | Medio | ✅ Finalizado |
| K | Achievements / insignias | 🟢 Baja | ~7–9 h | Medio | ✅ Finalizado |
| K.1 | Insignias de comportamiento (última hora / indecisión / confianza) | 🟢 Baja | ~5–7 h | Bajo | ✅ Finalizado |

**Orden de implementación sugerido:** I → J → K (I es el que más aprovecha infraestructura ya construida). K.1 depende de que K ya esté implementado (lo está).

Los tres módulos se diseñan **mobile-first** (base = layout de teléfono, se expande con `@media (min-width: 641px)`, mismo breakpoint que ya usan `Champion/Index.razor.css` y `MatchPredictionsSheet.razor.css`) y reutilizan el sistema visual ya existente en el proyecto en vez de inventar uno nuevo:
- **Tokens de diseño:** `wwwroot/css/theme.css` (`--q-navy`, `--q-gold`, `--q-blue`, `--q-green`, `--q-red`, `--q-sh-*` sombras, `--q-r-*` radios, `--q-g-*` gradientes).
- **Animaciones:** `wwwroot/css/animations.css` ya trae `fadeInUp`, `fadeInScale`, `popIn`, `pulseGold`, `slideInLeft`, `livePulse`, `shimmer` + las clases utilitarias `.anim-fade-in-up`, `.anim-fade-in-scale`, `.anim-d1`…`.anim-d6` (delays escalonados) y `.skeleton*` para loading states. Los tres módulos nuevos reutilizan estas keyframes en vez de definir animaciones propias.
- **Bottom sheet:** `Components/Shared/MatchPredictionsSheet.razor(.css)` ya implementa el patrón de hoja inferior (overlay con blur + panel `slideUpSheet` + handle + fondo `--q-navy`) que es el estándar táctil de la app para mostrar detalle sin navegar a otra página. Los tres módulos reutilizan este mismo componente/patrón en vez de tooltips o popovers, que son poco fiables en touch.

---

## Módulo I — Gráfica de evolución de posición ✅ Finalizado

### Objetivo

Mostrar cómo cambió la posición de cada jugador en la tabla, partido a partido, a lo largo del torneo — no solo el delta contra el snapshot anterior (que ya existe en Standings vía ▲/▼/─), sino la serie completa.

### Contexto técnico

`StandingsSnapshot` (`src/Quiniela.Data/Entities/StandingsSnapshot.cs`) ya guarda `Position`/`Points` por `(PoolId, MatchId, UserId)` cada vez que `ScoringService.SaveSnapshotAsync` corre (`src/Quiniela.Web/Services/ScoringService.cs:64`). Hoy el único consumidor es `StandingsService.GetLastSnapshotPositionsAsync` (`StandingsService.cs:91`), que solo lee el snapshot **más reciente**. No existe ningún método que devuelva la serie histórica completa — este módulo es 100% aprovechamiento de datos ya persistidos, sin nuevas migraciones.

Como se decidió en el Módulo F original, no hay backfill retroactivo: la serie empieza a partir del primer partido finalizado después de que ese módulo se activó. Este módulo hereda esa misma limitación (no hay nada que hacer al respecto, es un dato de origen).

### Nuevo método en StandingsService

```csharp
public record PositionPoint(int MatchId, DateTime KickoffUtc, int Position);

public async Task<Dictionary<int, List<PositionPoint>>> GetPositionHistoryAsync(int poolId)
{
    await using var db = await dbFactory.CreateDbContextAsync();

    var snapshots = await db.StandingsSnapshots
        .Include(s => s.Match)
        .Where(s => s.PoolId == poolId)
        .OrderBy(s => s.Match.KickoffUtc)
        .ToListAsync();

    return snapshots
        .GroupBy(s => s.UserId)
        .ToDictionary(
            g => g.Key,
            g => g.Select(s => new PositionPoint(s.MatchId, s.Match.KickoffUtc, s.Position)).ToList());
}
```

Ordenar por `Match.KickoffUtc` (no por `SavedAt`) es importante: `SavedAt` refleja cuándo el admin capturó/corrigió el resultado, que puede no coincidir con el orden cronológico del torneo si se corrige un partido tarde (como en el bug documentado en `docs/fixes.md`, Fix 2).

### Visualización

Página nueva con un renglón por jugador, cada uno con un mini-gráfico de línea generado en **SVG inline** (mismo espíritu que el mini-gráfico de barras CSS puro del Módulo E — sin librería de charts):

```
admin      ●───●───●───●   #1 → #1  (─)
jugador1   ●───●───●───●   #3 → #2  (▲+1)
jugador2   ●───●───●───●   #2 → #4  (▼-2)
```

- Eje Y invertido (posición 1 arriba, mayor número abajo).
- Cada punto es un partido finalizado, en orden cronológico (no hay eje de fechas, solo secuencia).
- El SVG se genera en C# calculando puntos `(x, y)` a partir de `PositionPoint` y armando un `<polyline>`; ancho responsivo (`viewBox` + `preserveAspectRatio="none"`) para que escale igual en mobile/desktop.

### Diseño visual y mobile-first

- **Mobile (base):** una tarjeta por jugador, apiladas verticalmente (`.card` con `--q-r`/`--q-sh-sm` de `theme.css`). Cabecera de la tarjeta con avatar + nombre + badge de posición actual (`#1 → #2`); debajo, el mini-gráfico SVG a ancho completo. Si la sala tiene muchos jugadores, scroll vertical de tarjetas (ya contemplado en el criterio de aceptación original).
- **Desktop (`@media (min-width: 641px)`):** grid de 2 columnas de tarjetas (mismo patrón que `.champ-grid` en `Champion/Index.razor.css`), para aprovechar el ancho sin volverse una tabla densa.
- **Animación de entrada:** la línea se "dibuja" al montar el componente con la técnica clásica de SVG (`stroke-dasharray` = longitud total del `polyline`, transición de `stroke-dashoffset` de esa longitud a `0` en ~0.6s `ease-out`). Los puntos (círculos) aparecen con `popIn` (de `animations.css`) escalonados por índice usando `animation-delay` proporcional a la posición en la serie (no las clases fijas `.anim-d1..d6`, que no alcanzan para series largas).
- **Badges de posición:** la flecha ▲ usa `--q-green`, ▼ usa `--q-red`, `─` usa `--q-muted`; si el jugador terminó en el puesto #1, el badge lleva `pulseGold` para destacarlo sutilmente. Las tarjetas en sí entran con `.anim-fade-in-up` + `.anim-d1`…`.anim-d6` escalonadas por orden de posición actual (el líder entra primero).
- **Detalle al tocar un punto:** en vez de un tooltip (poco fiable en touch), tocar un punto abre el mismo componente de **bottom sheet** que usa `MatchPredictionsSheet` (`slideUpSheet`, overlay con blur, fondo `--q-navy`) mostrando el partido, fecha y posición de ese punto — reemplaza la necesidad de un eje de fechas visible en el gráfico.
- **Loading state:** mientras carga el historial, placeholder con `.skeleton-card` (de `animations.css`) por cada tarjeta esperada.

### Archivos a crear / modificar

| Archivo | Cambio |
|---|---|
| `Services/StandingsService.cs` | Agregar `GetPositionHistoryAsync` |
| `Components/Pages/Standings/History.razor(.css)` | **Nueva página** en `/pools/{poolId}/standings/history` |
| `Components/Pages/Standings/Index.razor` | Botón/link "Ver evolución" |

### Criterio de aceptación

- Con 0 o 1 snapshot guardado: mensaje "Aún no hay suficiente historial" (se necesitan al menos 2 partidos finalizados con snapshot).
- El orden de los puntos es cronológico por `KickoffUtc`, no por el orden en que se capturaron/corrigieron resultados.
- La posición se muestra invertida (1 arriba).
- Funciona con salas de cualquier tamaño sin romper el layout (scroll vertical entre jugadores si hay muchos).
- El layout base (mobile) es una columna de tarjetas; a partir de 641px se expande a grid de 2 columnas sin romper el SVG.

### Estimación: 5–6 horas

---

## Módulo J — Head-to-head entre dos jugadores ✅ Finalizado

### Objetivo

Comparar, partido finalizado por partido finalizado, los pronósticos de dos miembros de la misma sala — quién le "ganó" a quién en cada partido y el acumulado total. Es la funcionalidad más directamente diseñada para generar debate entre amigos, en la misma línea que el Módulo B (ver pronósticos de todos).

### Diseño de la vista

```
jugador1  vs  jugador2      (Sala "MX")
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Partido            jugador1        jugador2
MEX 2–1 POL       🇲🇽 MEX ✓ +3    Empate ✗  0
ARG 1–1 ARA        Empate ✓ +3    🇦🇷 ARG ✗  0
...
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Total puntos           14 pts          9 pts
Partidos ganados        7               3        •  5 empates
```

"Ganar" un partido en el head-to-head = obtener más puntos que el rival en ese partido puntual (no que ambos hayan acertado).

### Nuevo servicio: HeadToHeadService

```csharp
public record H2HRow(
    int MatchId,
    string MatchLabel,
    DateTime KickoffUtc,
    char? PredA, int PtsA,
    char? PredB, int PtsB);

public class HeadToHeadService(IDbContextFactory<QuinielaDbContext> dbFactory)
{
    public async Task<List<H2HRow>> CompareAsync(int poolId, int userAId, int userBId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var matches = await db.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.Status == MatchStatus.Finalizado
                     && m.Predictions.Any(p => p.PoolId == poolId && (p.UserId == userAId || p.UserId == userBId)))
            .OrderBy(m => m.KickoffUtc)
            .ToListAsync();

        var matchIds = matches.Select(m => m.Id).ToList();
        var preds = await db.Predictions
            .Where(p => p.PoolId == poolId
                     && matchIds.Contains(p.MatchId)
                     && (p.UserId == userAId || p.UserId == userBId))
            .ToListAsync();

        return matches.Select(m =>
        {
            var predA = preds.FirstOrDefault(p => p.MatchId == m.Id && p.UserId == userAId);
            var predB = preds.FirstOrDefault(p => p.MatchId == m.Id && p.UserId == userBId);
            return new H2HRow(
                m.Id,
                $"{m.HomeTeam?.ShortCode ?? m.HomeSlotLabel} vs {m.AwayTeam?.ShortCode ?? m.AwaySlotLabel}",
                m.KickoffUtc,
                predA?.PredOutcome, predA?.Points ?? 0,
                predB?.PredOutcome, predB?.Points ?? 0);
        }).ToList();
    }
}
```

Partidos donde solo uno de los dos pronosticó **sí se incluyen** (el que no pronosticó queda con `PredX = null`, `PtsX = 0`) para no distorsionar el total acumulado, pero la UI los marca visualmente distinto (ej. atenuados) en vez de contarlos como "ganados".

### Acceso a la vista

Desde `Standings/Index.razor`: un botón "Comparar" que en mobile abre el selector de dos jugadores como **bottom sheet** (mismo patrón de `MatchPredictionsSheet`, con lista tocable de avatares en vez de dos `<select>` nativos — más cómodo con el pulgar que dos dropdowns), y en desktop (`@media (min-width: 641px)`) puede mostrarse como un par de dropdowns inline junto al botón. Navega a `/pools/{poolId}/standings/vs?a={userIdA}&b={userIdB}`.

### Diseño visual y mobile-first

- **Mobile (base):** en vez de la tabla ancha del boceto original (que obliga a scroll horizontal en pantallas chicas), cada partido se muestra como una **tarjeta apilada** (mismo lenguaje visual que `MatchCard.razor`): nombre/bandera del partido arriba, y debajo un layout de dos columnas compacto con el pronóstico y puntos de cada jugador lado a lado dentro de la misma tarjeta (pill verde si acertó, gris/rojo si no, igual que los `.mps-pred-pill` de `MatchPredictionsSheet`).
- **Cabecera sticky:** avatar + nombre de cada jugador y el marcador acumulado corriente, fijos arriba mientras se hace scroll por los partidos (`position: sticky; top: 0`), para no perder de vista quién va ganando.
- **Barra de totales sticky al fondo:** reutiliza el patrón `.champ-save-bar` de `Champion/Index.razor.css` (`position: sticky; bottom: 0` con gradiente de desvanecido) mostrando el resumen final (puntos totales, partidos ganados, empates) siempre visible.
- **Desktop (`@media (min-width: 641px)`):** con más ancho disponible sí se usa la tabla de dos columnas del boceto original, sin necesidad de apilar tarjetas.
- **Animaciones:** cada fila/tarjeta entra con `.anim-fade-in-up` escalonada (`.anim-d1`…`.anim-d6`, cíclico si hay más de 6 partidos). La pill del jugador que "ganó" ese partido puntual hace `popIn` al entrar. El badge "VS" del encabezado usa `pulseGold` de forma sutil. Los totales de la barra inferior no aparecen de golpe: se animan con un conteo ascendente (tween 0 → valor final en ~600ms vía JS interop simple, sin librería) cada vez que cambia el par de jugadores comparado.

### Archivos a crear / modificar

| Archivo | Cambio |
|---|---|
| `Services/HeadToHeadService.cs` | **Nuevo** |
| `Components/Pages/Standings/Versus.razor(.css)` | **Nueva página** en `/pools/{poolId}/standings/vs` |
| `Components/Pages/Standings/Index.razor` | Botón "Comparar" + selector de dos jugadores |
| `Program.cs` | Registrar `HeadToHeadService` |

### Criterio de aceptación

- Solo compara partidos ya finalizados de la sala.
- Los totales de puntos por partido coinciden exactamente con los que ya muestra Standings para esos mismos jugadores.
- Funciona igual en partidos de grupos (solo `PredOutcome`/`Points`) y de KO (implícito en `Points`, que ya incluye `PtsResult + PtsInstance`).
- No se puede seleccionar el mismo jugador dos veces (validación simple en el selector).
- Si ninguno de los dos pronosticó ningún partido en común: mensaje "Sin partidos para comparar".
- En mobile, la comparación se ve como tarjetas apiladas sin scroll horizontal; en desktop (≥641px) se ve como tabla de dos columnas.

### Estimación: 5–7 horas

---

## Módulo K — Achievements / insignias ✅ Finalizado

### Objetivo

Reconocimientos automáticos y divertidos por jugador, calculados sobre datos que la app ya tiene (sin nueva tabla persistida), en una "vitrina de trofeos" visible para toda la sala.

### Diseño

Catálogo fijo de insignias (no configurable por el admin en v1), cada una con ícono, nombre, descripción y criterio calculado en código a partir de `Predictions`, `StandingsSnapshot` y `ChampionPrediction` — reutilizando en gran parte lo que ya calcula `PlayerStatsService` (`BestStreak`, `CorrectResults`, `KoCorrect`, etc.).

| Insignia | Criterio |
|---|---|
| 🔥 Racha de fuego | `BestStreak >= 5` (aciertos de resultado consecutivos) |
| 🎯 Francotirador KO | 100% de aciertos de avance en KO, con al menos 3 pronósticos KO |
| 👑 Vidente | Acertó al campeón del mundial (`ChampionPrediction.Points > 0`, solo tras finalizar la Final) |
| 📈 La Remontada | Subió 3 o más posiciones entre dos snapshots consecutivos (usa `StandingsSnapshot`, Módulo F) |
| 🥇 Puntero eterno | Estuvo en la posición #1 en al menos el 70% de los snapshots guardados de la sala |
| 🐢 Modo tortuga | 0 aciertos de resultado tras 10+ pronósticos (insignia honorífica/irónica) |
| 🎪 El Bipolar | Alternó acierto/fallo de resultado en 5+ partidos consecutivos sin repetir el mismo patrón dos veces seguidas (secuencia estrictamente alternada) |
| 🪦 El Traidor | Pronosticó la eliminación o derrota de un equipo en algún partido de grupos/octavos y ese mismo equipo terminó siendo el campeón del mundial (`ChampionPrediction` ≠ campeón real) |

### Nuevo servicio: AchievementsService

```csharp
public record Achievement(string Icon, string Name, string Description);

public class AchievementsService(
    IDbContextFactory<QuinielaDbContext> dbFactory,
    PlayerStatsService statsService)
{
    public async Task<Dictionary<int, List<Achievement>>> GetForPoolAsync(int poolId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var members = await db.PoolMembers
            .Where(m => m.PoolId == poolId)
            .Select(m => m.UserId)
            .ToListAsync();

        var result = new Dictionary<int, List<Achievement>>();
        foreach (var userId in members)
        {
            var stats = await statsService.GetAsync(userId, poolId);
            var badges = new List<Achievement>();

            if (stats.BestStreak >= 5)
                badges.Add(new("🔥", "Racha de fuego", "5+ aciertos de resultado seguidos"));
            if (stats.KoPredictions >= 3 && stats.KoCorrect == stats.KoPredictions)
                badges.Add(new("🎯", "Francotirador KO", "100% de aciertos de avance en eliminatorias"));
            if (stats.TotalPredictions >= 10 && stats.CorrectResults == 0)
                badges.Add(new("🐢", "Modo tortuga", "Ni una sola de resultado, con honor"));
            // Vidente, Remontada, Puntero eterno, El Bipolar y El Traidor requieren
            // consultas propias (ChampionPrediction / StandingsSnapshot / secuencia de
            // aciertos por partido) — ver detalle en el cuerpo del servicio.

            result[userId] = badges;
        }
        return result;
    }
}
```

Las insignias "Vidente", "La Remontada", "Puntero eterno", "El Bipolar" y "El Traidor" necesitan datos que `PlayerStats` no expone hoy (`ChampionPrediction.Points`, serie completa de `StandingsSnapshot`, secuencia cronológica de aciertos por partido) — se resuelven con consultas adicionales dentro del mismo servicio, reutilizando `GetPositionHistoryAsync` del Módulo I para "La Remontada"/"Puntero eterno" si ese módulo ya está implementado (si no, se calcula el mismo query localmente). "El Bipolar" reutiliza la misma secuencia ordenada por `KickoffUtc` que ya usa `PlayerStatsService.BestStreak`, solo que en vez de buscar una racha busca alternancia estricta. "El Traidor" cruza `ChampionPrediction` con los `Predictions` de grupos/octavos del equipo campeón real.

### UI: vitrina de trofeos y cómo el usuario se entera del criterio de cada insignia

Página nueva `/pools/{poolId}/achievements`: una tarjeta por jugador con sus insignias como chips (ícono + nombre corto). El chip solo no basta para explicar el criterio, así que se resuelve en dos capas:

1. **Detalle al tocar el chip** — cada chip es interactivo: tocarlo (mobile) o hacerle clic (desktop) abre el mismo componente de **bottom sheet** que usa `MatchPredictionsSheet` (en vez de un popover/tooltip, poco fiables en touch) con ícono grande + nombre + `Description` completa de esa insignia (ej. "5+ aciertos de resultado seguidos"). Esto cubre el caso de "ya tengo la insignia, ¿por qué la gané?".
2. **Catálogo completo de insignias** — un link "¿Qué significa cada insignia? ℹ️" en la parte superior de la página lleva a una lista de **todas** las insignias del catálogo (las 8), incluyendo las que el jugador todavía no tiene (mostradas en gris/atenuadas), cada una tocable y abriendo el mismo bottom sheet de detalle. Esto cubre el caso de "quiero saber qué insignias existen y cómo conseguirlas", clave para insignias irónicas como 🐢 o 🎪 que de otro modo nadie sabría que existen.

Se deja fuera de la tabla de Standings (ya cargada) para no saturarla; un link "🏆 Logros" en Standings apunta a la vitrina.

### Diseño visual y mobile-first

- **Mobile (base):** grid de 2 columnas de tarjetas de insignia (mismo patrón que `.champ-grid`/`.champ-cell` de `Champion/Index.razor.css`: borde + fondo blanco + `border-radius` de `--q-r-sm`). Insignias no obtenidas se muestran en escala de grises con opacidad reducida (mismo tratamiento que `.champ-flag-eliminated`), las obtenidas llevan color: dorado (`--q-g-gold`) para las de prestigio (👑 Vidente, 🥇 Puntero eterno), verde (`--q-g-green`) para rachas positivas (🔥, 🎯, 📈), y un tono neutro/gris-azulado para las irónicas (🐢, 🎪, 🪦) que las distingue como "de broma" sin ser negativas visualmente.
- **Desktop (`@media (min-width: 641px)`):** el grid se expande a 4 columnas, igual que hace `.champ-grid` en ese mismo breakpoint.
- **Animación de "desbloqueo":** cuando una insignia se obtiene por primera vez (se detecta comparando contra la última vista, guardada en `localStorage` por pool+usuario — sin nueva tabla en BD), esa tarjeta hace una entrada especial al cargar la página: `popIn` combinado con un anillo `pulseGold` sostenido ~1.2s para que resalte sobre las demás. El resto de las insignias (ya vistas antes) entran con el `.anim-fade-in-up` + `.anim-d1`…`.anim-d6` estándar, en cascada por fila.
- **Detalle táctil:** tocar cualquier tarjeta (obtenida o no) abre el bottom sheet de descripción mencionado arriba, con la misma animación `slideUpSheet` y fondo `--q-navy` que ya usa `MatchPredictionsSheet`.
- **Loading state:** placeholders `.skeleton-card` (de `animations.css`) mientras se calculan las insignias del jugador on-demand.

### Archivos a crear / modificar

| Archivo | Cambio |
|---|---|
| `Services/AchievementsService.cs` | **Nuevo** |
| `Components/Pages/Achievements/Index.razor(.css)` | **Nueva página** en `/pools/{poolId}/achievements` (vitrina + catálogo completo) |
| `Components/Pages/Standings/Index.razor` | Link "🏆 Logros" |
| `Program.cs` | Registrar `AchievementsService` |

### Criterio de aceptación

- Catálogo de insignias es el mismo para todas las salas (no hay configuración por pool en v1).
- Un jugador puede tener 0 o varias insignias a la vez.
- "La Remontada" y "Puntero eterno" requieren al menos 2 snapshots; si la sala no tiene historial suficiente, simplemente no se otorgan (sin error, sin insignia).
- Cada chip de insignia obtenida es interactivo y muestra su descripción completa al tocar/hacer clic (no solo un ícono sin contexto).
- Existe una vista de catálogo completo con las 8 insignias y su criterio, visible aunque el jugador no las tenga todas.
- No se agrega ninguna tabla nueva a la BD — todo se calcula on-demand a partir de datos existentes.
- El grid es de 2 columnas en mobile y 4 en desktop (≥641px); una insignia recién obtenida se distingue visualmente de las ya vistas anteriormente.

### Estimación: 7–9 horas

---

## Apéndice K.1 — Insignias de comportamiento (Última hora / Indecisión / Confianza) ✅ Finalizado

### Objetivo

Tres insignias nuevas que, a diferencia de las 8 originales, no miden acierto/desempeño sino **comportamiento al pronosticar**:

| Insignia | Criterio |
|---|---|
| ⏱️ **Gol Agónico** | El cambio final de su pronóstico ocurrió a 30 minutos o menos del kickoff, en 2 o más partidos |
| 🎰 **Modo Tragamonedas** | Cambió realmente su pronóstico 3 veces o más en el mismo partido, en 2 o más partidos distintos |
| 🗿 **Dicho y Hecho** | Nunca cambió ninguno de sus pronósticos, con al menos 10 hechos |

Nombres alternativos si alguno no convence: para Gol Agónico, "El Bombero" 🚒; para Modo Tragamonedas, "El Indeciso" 🔄; para Dicho y Hecho, "Sangre Fría" 🧊 o "Roca Firme" ✅.

### Por qué se necesita una tabla nueva (a diferencia del resto del Módulo K)

Las 8 insignias originales se calculan on-demand porque toda la información que necesitan ya existe en `Predictions`, `StandingsSnapshot` y `ChampionPrediction`. Estas 3 insignias son distintas: dependen de **cuántas veces y cuándo** un jugador tocó su pronóstico, y hoy esa información no se conserva.

`Prediction` (`src/Quiniela.Data/Entities/Prediction.cs`) es una fila por `(UserId, PoolId, MatchId)` con solo `CreatedAt`/`UpdatedAt`. `PredictionService.UpsertAsync` (`src/Quiniela.Web/Services/PredictionService.cs:74-98`) ya hace upsert real (inserta la primera vez, actualiza en el sitio las siguientes) — **ese comportamiento no cambia**. Lo que falta es un registro histórico de cada valor que pasó por ahí, porque `UpdatedAt` se sobrescribe en cada guardado y no dice cuántos cambios reales hubo ni cuáles fueron.

Importante: hoy `UpsertAsync` sobreescribe `PredOutcome`/`PredInstance`/`UpdatedAt` en **cada** guardado, incluso si el jugador reenvía el mismo valor sin modificarlo. Para que "Modo Tragamonedas" no se infle con reenvíos accidentales, un "cambio" solo cuenta cuando el valor nuevo es distinto al que estaba guardado.

### Nueva entidad: `PredictionHistory`

Tabla de solo-inserción (append-only): cada fila es un valor que estuvo vigente en algún momento. La primera fila (creación) y cada cambio real generan una fila nueva; un reenvío del mismo valor no genera fila.

```csharp
// src/Quiniela.Data/Entities/PredictionHistory.cs
public class PredictionHistory
{
    public int Id { get; set; }
    public int PredictionId { get; set; }
    public char PredOutcome { get; set; }
    public MatchDecidedIn? PredInstance { get; set; }
    public DateTime ChangedAt { get; set; }

    public Prediction Prediction { get; set; } = null!;
}
```

Registro en `QuinielaDbContext.cs` (mismo patrón que las entidades existentes):

```csharp
public DbSet<PredictionHistory> PredictionHistories => Set<PredictionHistory>();

// en OnModelCreating:
modelBuilder.Entity<PredictionHistory>(e =>
{
    e.Property(h => h.PredOutcome).HasColumnType("char(1)");
    e.HasIndex(h => h.PredictionId);
    e.HasOne(h => h.Prediction)
        .WithMany()
        .HasForeignKey(h => h.PredictionId)
        .OnDelete(DeleteBehavior.Cascade); // único padre posible, sin conflicto de cascada múltiple
});
```

Migración: `dotnet ef migrations add AddPredictionHistory --project src/Quiniela.Data --startup-project src/Quiniela.Web`. Como el resto del proyecto, se aplica sola en el arranque (`Program.cs` ya llama `Database.MigrateAsync()`).

### Modificación a `PredictionService.UpsertAsync`

Se agrega una fila de historial en la creación y, en la actualización, solo si el valor realmente cambió:

```csharp
if (existing is not null)
{
    bool realChange = existing.PredOutcome != outcome || existing.PredInstance != predInstance;
    existing.PredOutcome = outcome;
    existing.PredInstance = predInstance;
    existing.UpdatedAt = now;

    if (realChange)
        db.PredictionHistories.Add(new PredictionHistory
        {
            PredictionId = existing.Id,
            PredOutcome = outcome,
            PredInstance = predInstance,
            ChangedAt = now
        });
}
else
{
    var prediction = new Prediction { /* ...como hoy... */ };
    db.Predictions.Add(prediction);
    await db.SaveChangesAsync(); // necesario para obtener prediction.Id antes de crear el historial

    db.PredictionHistories.Add(new PredictionHistory
    {
        PredictionId = prediction.Id,
        PredOutcome = outcome,
        PredInstance = predInstance,
        ChangedAt = now
    });
    await db.SaveChangesAsync();
}
```

### Cálculo de las insignias en `AchievementsService`

Se agrega el catálogo y una consulta agrupada por predicción, sin nuevo servicio:

```csharp
new("last-minute", "⏱️", "Gol Agónico", "Envió el cambio final de su pronóstico a 30 minutos o menos del kickoff, en 2 o más partidos", AchievementCategory.Ironic),
new("slot-machine", "🎰", "Modo Tragamonedas", "Cambió su pronóstico 3 veces o más en el mismo partido, en 2 o más partidos distintos", AchievementCategory.Ironic),
new("sure-shot", "🗿", "Dicho y Hecho", "Nunca cambió ninguno de sus pronósticos, con al menos 10 hechos", AchievementCategory.Positive),
```

```csharp
var historyRows = await db.PredictionHistories
    .Include(h => h.Prediction).ThenInclude(p => p.Match)
    .Where(h => h.Prediction.PoolId == poolId)
    .ToListAsync();

var changesByUser = historyRows
    .GroupBy(h => h.Prediction.UserId)
    .ToDictionary(g => g.Key, g => g
        .GroupBy(h => h.PredictionId)
        .Select(pg => new
        {
            ChangeCount = pg.Count() - 1, // filas totales - la inicial
            FinalChangeAt = pg.Max(h => h.ChangedAt),
            KickoffUtc = pg.First().Prediction.Match.KickoffUtc
        })
        .ToList());

// dentro del foreach (userId in members):
var changes = changesByUser.GetValueOrDefault(userId, []);

if (changes.Count(c => c.KickoffUtc - c.FinalChangeAt <= TimeSpan.FromMinutes(30)) >= 2)
    badges.Add(AchievementCatalog.Get("last-minute"));

if (changes.Count(c => c.ChangeCount > 2) >= 2)
    badges.Add(AchievementCatalog.Get("slot-machine"));

if (changes.Count >= 10 && changes.All(c => c.ChangeCount == 0))
    badges.Add(AchievementCatalog.Get("sure-shot"));
```

Nota de diseño: a diferencia de "Modo tortuga"/"Francotirador KO" (que usan `PlayerStatsService.TotalPredictions`, limitado a partidos ya finalizados porque necesitan saber si acertó), estas 3 insignias son sobre el **comportamiento al pronosticar**, no sobre acierto — por eso cuentan sobre **todos** los pronósticos del jugador en la sala, hayan finalizado o no. Esto es una decisión de diseño razonable pero no explícitamente confirmada contigo; si prefieres limitarlo solo a partidos finalizados (por consistencia estricta con el resto del catálogo), es un cambio de una línea (agregar `&& h.Prediction.Match.Status == MatchStatus.Finalizado` al `Where`).

No requiere cambios en `Components/Pages/Achievements/Index.razor`: la vista ya renderiza el catálogo y las insignias obtenidas de forma genérica a partir de `AchievementCatalog.All`, así que las 3 nuevas entradas aparecen automáticamente en el grid y en el catálogo completo.

### Archivos a crear / modificar

| Archivo | Cambio |
|---|---|
| `Entities/PredictionHistory.cs` | **Nuevo** |
| `QuinielaDbContext.cs` | `DbSet<PredictionHistory>` + configuración en `OnModelCreating` |
| Migración EF (`AddPredictionHistory`) | **Nueva** |
| `Services/PredictionService.cs` | `UpsertAsync` registra historial en insert y en cambios reales |
| `Services/AchievementsService.cs` | 3 entradas nuevas en `AchievementCatalog.All` + lógica de cálculo |

### Criterio de aceptación

- Reenviar el mismo H/D/A (o la misma instancia en KO) sin cambiarlo no genera fila de historial ni cuenta como "cambio" para ninguna de las 3 insignias.
- "Gol Agónico" y "Modo Tragamonedas" requieren que el patrón ocurra en 2 o más partidos distintos (no se otorgan por una sola coincidencia).
- "Dicho y Hecho" requiere el mínimo de 10 pronósticos hechos (igual que "Modo tortuga") y que **ninguno** haya sido editado.
- No hay backfill retroactivo: el historial empieza a contar desde que se despliega este cambio, igual que el resto de módulos basados en series temporales (Módulo I, F). Pronósticos ya guardados antes del deploy cuentan como "1 sola vez" (fila inicial sintética) o quedan fuera del cálculo hasta su próxima edición — a definir al implementar, pero no bloquea el resto del criterio.
- Las 3 insignias nuevas se integran al mismo catálogo/vitrina del Módulo K sin cambios de UI.

### Estimación: 5–7 horas

---

## Resumen de archivos nuevos de este documento

| Archivo | Módulo |
|---|---|
| `Services/HeadToHeadService.cs` | J |
| `Services/AchievementsService.cs` | K |
| `Components/Pages/Standings/History.razor(.css)` | I |
| `Components/Pages/Standings/Versus.razor(.css)` | J |
| `Components/Pages/Achievements/Index.razor(.css)` | K |

---

## Orden de implementación sugerido

```
I (evolución de posición, 5-6h)
  └─> J (head-to-head, 5-7h)
        └─> K (achievements, 7-9h)
```

Total estimado: **~17–22 horas** de trabajo.
