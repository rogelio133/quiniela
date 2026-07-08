# 08 — Insignias "Mejor del día" / "Peor del día"

**Fecha:** 2026-07-08
**Estado:** ✅ Implementado (2026-07-08) — M1, M2, M3, M4 y N10 completos. Verificado con `dotnet build` limpio (0 errores); sin verificación en navegador ni disparo del endpoint `/api/notify/check` en vivo en esta sesión.
**Contexto:** Módulos 0–8, A–L, I/J/K/K.1 y notificaciones N0–N4/N6–N9 completos (solo N5 pendiente de `07_notificaciones.md`). Este documento define dos insignias diarias con conteo de medallas, la notificación nocturna que las anuncia (9:30 PM CDMX) y un ajuste de navegación: mover el botón de Logros de la tabla de posiciones al detalle de la sala.

> **Ajuste 2026-07-08 (misma fecha, segunda iteración):** cambia la regla de empates — la medalla de cada lado ahora exige **ganador único** (un empate en el máximo o en el mínimo anula ese lado, de forma independiente) — y se agrega el módulo **M4**: la tarjeta "El peor del día" en el Resumen diario, junto a la de "El mejor del día". Nota de interpretación: el usuario pidió agregar los peores del día "al módulo de evolución"; la vista día-por-día donde ya viven los mejores del día es el **Resumen diario** (`/pools/{poolId}/daily-summary`) — la página de Evolución (`/standings/history`) solo grafica posiciones por partido, sin concepto de día — así que la tarjeta se colocó ahí.

---

## Decisiones de producto (confirmadas con el usuario)

| Tema | Decisión |
|------|----------|
| ¿Quién participa en "Peor del día"? | **Todos los miembros de la sala.** No pronosticar ese día cuenta como 0 puntos (y muy probablemente te lleva el "Peor del día"). |
| Empates | **La medalla exige ganador único** (ajuste 2026-07-08; la regla original era "todos los empatados ganan"). Si 2+ jugadores comparten el máximo, ese día **nadie** recibe "Mejor del día"; si 2+ comparten el mínimo, nadie recibe "Peor del día". Los dos lados son **independientes**: un día puede otorgar solo mejor, solo peor, ambos o ninguno. Si TODOS empatan (mejor = peor, incluye "nadie pronosticó" y salas de 1 miembro), no se otorga ninguna. |
| ¿Dónde se ven "los peores del día"? | Tarjeta **"El peor del día"** en el Resumen diario, debajo de "El mejor del día" (M4). Es **informativa**: con empate muestra a todos los empatados ("Los peores del día") aunque ese día no se otorgue medalla — la tarjeta informa el hecho, la medalla premia solo sin empate. |
| Destinatarios de la notificación 9:30 PM | **Ambas:** el mejor y el peor reciben su versión personal; el **resto de la sala** recibe un anuncio con nombres (burla pública). |
| Persistencia del conteo de medallas | **On-demand, sin tabla nueva.** Se recalcula siempre desde `Predictions` + `Matches` finalizados agrupados por día — mismo patrón que el resto de insignias. Se auto-corrige si el admin corrige un marcador viejo. |
| Textos | Mejor: "Hoy amaneciste brujo" · Peor: "Ouch… el peor del día (moneda al aire)" · Anuncio: "Última hora" (ver N10). |

---

## Definición del cálculo "mejor / peor del día"

Regla única compartida por la vitrina de logros (medallas) y la notificación N10:

1. **El día** es la fecha local **America/Mexico_City** del `KickoffUtc` de cada partido (misma TZ fija que `DailySummaryService`/`NotificationCheckService`; sin DST desde 2022).
2. Solo cuentan días con **≥ 1 partido `Finalizado`**.
3. **Puntos del día por miembro** (por sala): `SUM(Points)` de sus `Predictions` en esa sala para los partidos finalizados del día. Miembro sin pronóstico ese día = **0 puntos** (sí participa). El día de la Final se suman además los puntos de `ChampionPrediction` — mismo criterio que los `DayLeaders` del Módulo L (`DailySummaryService`).
4. **Mejor del día** = el miembro con el máximo **solo si es único**; con empate en el máximo ese lado no se otorga. **Peor del día** = el miembro con el mínimo **solo si es único**; con empate en el mínimo ese lado no se otorga. Los lados son independientes (puede haber mejor sin peor y viceversa).
5. Si `max == min` (todos empatados) o los **dos** lados quedan empatados: **no se otorga nada** ese día.
6. Es **evento de sala**: se calcula por pool, un mismo día puede tener mejor/peor distintos en salas distintas.

Notas del enfoque on-demand:

- El **día en curso** cuenta en vivo en la vitrina: a media tarde puedes "ir ganando" la medalla del día y perderla con el partido de la noche. La notificación de las 9:30 PM es el "cierre oficial" del día.
- Si el admin corrige un marcador viejo, las medallas se **auto-corrigen** en la siguiente visita a la vitrina. La notificación ya enviada no se reenvía ni se retracta — la vitrina es la verdad, la notificación es la invitación (mismo principio que N9).

---

## Resumen de módulos

| # | Hecho | Módulo | Esfuerzo |
|---|:-----:|--------|----------|
| M1 | [x] | Mover botón "Logros" de Standings a Pool Detail | ~15 min |
| M2 | [x] | `DailyAwardService` — cálculo mejor/peor por día | ~2 h |
| M3 | [x] | Insignias 🌞/🥴 + medallas 🏅 en la página de Logros | ~2 h |
| M4 | [x] | Tarjeta "El peor del día" en Resumen diario | ~1 h |
| N10 | [x] | Notificación 9:30 PM CDMX (personal + anuncio) | ~2 h |

**Orden sugerido:** M1 (independiente) → M2 → M3 → N10 (N10 y M3 dependen de M2). M4 es independiente (vive en `DailySummaryService`, no usa `DailyAwardService`).

---

## Módulo M1 — Mover botón "Logros" al detalle de la sala

### Objetivo

El acceso a `/pools/{poolId}/achievements` hoy vive en la cabecera de la tabla de posiciones (`Standings/Index.razor`, junto a "Ver evolución"/"Comparar"/"Actualizar"). Se **mueve** (no se copia) a la barra de botones de `Pools/Detail.razor`, junto a Pronosticar / Mis pronósticos / Tabla / Campeón / Resumen diario.

### Implementación

1. **Quitar** de `Components/Pages/Standings/Index.razor` el `<a>` de Logros:

```razor
@* ELIMINAR de la cabecera de Standings *@
<a href="/pools/@PoolId/achievements" class="btn btn-outline-secondary btn-sm">
    🏆 Logros
</a>
```

2. **Agregar** en `Components/Pages/Pools/Detail.razor`, dentro del `d-flex gap-2 mb-4 flex-wrap` existente (después de "📅 Resumen diario"):

```razor
<a href="/pools/@Id/achievements" class="btn btn-outline-success btn-sm">
    🎖️ Logros
</a>
```

Se cambia el emoji 🏆 → 🎖️ porque en Pool Detail el botón "🏆 Tabla" ya usa la copa y quedarían dos botones con el mismo ícono.

3. **Breadcrumb** de `Achievements/Index.razor`: hoy incluye el crumb "Tabla de posiciones" porque se llegaba desde ahí. Como el acceso ahora es desde el detalle de la sala, se elimina ese crumb intermedio (queda `Mis Salas → {sala} → Logros`).

### Criterios de aceptación

- [x] El botón ya no aparece en la tabla de posiciones
- [x] El botón aparece en el detalle de la sala y navega a `/pools/{poolId}/achievements`
- [x] El breadcrumb de Logros ya no pasa por "Tabla de posiciones"

---

## Módulo M2 — `DailyAwardService` (cálculo mejor/peor por día)

### Objetivo

Servicio único que implementa la regla de la sección "Definición del cálculo", consumido por la vitrina de logros (M3, conteo de medallas) y por la notificación (N10, día específico). Sin tabla nueva.

### Diseño

```csharp
// src/Quiniela.Web/Services/DailyAwardService.cs
public class DailyAwardService(IDbContextFactory<QuinielaDbContext> dbFactory)
{
    private static readonly TimeZoneInfo Tz =
        TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    public record DayAwards(
        DateOnly Day,
        int? BestUserId,     // null = empate en el máximo, ese lado no se otorga
        int? WorstUserId,    // null = empate en el mínimo, ese lado no se otorga
        int MaxPoints,
        int MinPoints);

    // Todos los días con >= 1 partido finalizado que otorgaron al menos una medalla
    // (mejor o peor únicos), incluido el día en curso. Base para el conteo de medallas.
    public Task<List<DayAwards>> GetAllAsync(int poolId);

    // Conteo de medallas por usuario: (veces mejor, veces peor)
    public Task<Dictionary<int, (int Best, int Worst)>> GetCountsAsync(int poolId);

    // Un día específico (lo usa N10 a las 21:30). Null si el día no otorga ninguna medalla.
    public Task<DayAwards?> GetForDayAsync(int poolId, DateOnly day);
}
```

> Ajuste 2026-07-08: `BestUserIds`/`WorstUserIds` (listas de empatados) se reemplazaron por `BestUserId`/`WorstUserId` nullable — la medalla exige ganador único por lado.

Algoritmo de `GetAllAsync` (los otros dos derivan de él):

1. Traer partidos `Finalizado` (`Id`, `KickoffUtc`) y agruparlos por fecha local CDMX (en memoria, ~100 partidos máximo — mismo patrón que `DailySummaryService.GetAsync` paso 1).
2. Traer los `UserId` de `PoolMembers` de la sala y las `Predictions` de la sala para esos partidos (`UserId`, `MatchId`, `Points`).
3. Por cada día: total por miembro = suma de sus `Points` de ese día, **0 si no tiene filas**. Si el día incluye el partido de `MatchStage.Final`, sumar `ChampionPrediction.Points` por usuario (mismo criterio que `DailySummaryService`).
4. `max`/`min` sobre TODOS los miembros; si `max == min` el día se descarta. Si no: `BestUserId` = el único miembro con `max` (null si el máximo está empatado), `WorstUserId` = el único miembro con `min` (null si el mínimo está empatado). Si ambos lados quedan null, el día se descarta.

Notas:

- `GetCountsAsync` solo agrega: `Best += 1` por cada día en que el usuario es `BestUserId` (ídem `Worst` con `WorstUserId`).
- Miembros que se unieron a la sala a media competencia cuentan desde siempre con 0 pts en días pasados — consecuencia directa de la decisión "no pronosticar = 0 pts"; se acepta (la sala real se formó completa antes del torneo).
- Registrar en `Program.cs` como `Scoped`, mismo patrón que el resto de servicios.

### Criterios de aceptación

- [x] Un día con máximo único y mínimo único produce exactamente un mejor y un peor
- [x] Empate en el máximo → `BestUserId = null` (nadie gana "Mejor del día"); empate en el mínimo → `WorstUserId = null`; los lados se otorgan de forma independiente
- [x] Miembro sin pronósticos ese día participa con 0 puntos
- [x] Día con todos empatados (incluye "nadie pronosticó") no otorga nada
- [x] El día de la Final incluye los puntos de campeón en los totales
- [x] Corregir un marcador viejo cambia los conteos en la siguiente lectura (sin estado guardado)

---

## Módulo M3 — Insignias 🌞/🥴 con medallas 🏅 en la página de Logros

### Objetivo

Dos insignias nuevas en el catálogo. En la vitrina, **debajo del nombre de la insignia** se muestra una medalla 🏅 **por cada día** en que el jugador fue el mejor/peor, calculadas on-demand con `DailyAwardService.GetCountsAsync`.

### Catálogo

```csharp
// AchievementCatalog.All — 2 entradas nuevas
new("daily-best",  "🌞", "Mejor del día",
    "Fue quien más puntos sumó en un día de partidos, sin empatar con nadie. Una medalla 🏅 por cada día ganado.",
    AchievementCategory.Positive),
new("daily-worst", "🥴", "Peor del día",
    "Fue quien menos puntos sumó en un día de partidos, sin empatar con nadie (no pronosticar cuenta como 0). Una medalla 🏅 por cada día... sufrido.",
    AchievementCategory.Ironic),
```

### Cambio de firma en `AchievementsService`

`GetForPoolAsync` hoy devuelve `Dictionary<int, List<Achievement>>`. Para llevar el conteo de medallas se envuelve en un record:

```csharp
public record EarnedBadge(Achievement Badge, int Medals); // Medals = 0 → sin fila de medallas

public async Task<Dictionary<int, List<EarnedBadge>>> GetForPoolAsync(int poolId)
```

- Las 11 insignias existentes se devuelven con `Medals = 0` (no cambia su lógica ni su render).
- `daily-best`/`daily-worst`: se obtienen si el conteo respectivo de `GetCountsAsync` es `>= 1`, con `Medals = conteo`.
- `AchievementsService` gana dependencia de `DailyAwardService` (inyección por constructor, como ya hace con `PlayerStatsService`/`StandingsService`).
- Llamadores a actualizar: `Achievements/Index.razor` (usa `.Select(a => a.Key)` en dos lugares) y `ScoringService.NotifyNewAchievementsAsync` (N4 — ver advertencia abajo).

### UI (`Achievements/Index.razor(.css)`)

- En la celda (`ach-cell`), debajo de `ach-cell-name`, si `Medals > 0`:

```razor
<span class="ach-cell-medals">
    @for (int i = 0; i < badge.Medals; i++) { <span>🏅</span> }
</span>
```

Render literal de una medalla por ocurrencia (decisión del usuario), con `flex-wrap` y tamaño reducido (~0.7rem) para que quepan varias filas. El máximo realista es ~25 días de jornada en el torneo, cabe sin cap.

- El **bottom sheet** de detalle muestra también la fila de medallas bajo la descripción (con el conteo del jugador seleccionado; en modo catálogo, el del usuario actual).
- La animación de "desbloqueo" (`localStorage` de claves vistas) funciona sin cambios para la **primera** vez que se obtiene la insignia; ganar una **medalla adicional** no re-dispara el glow (limitación aceptada — la clave no cambia, solo el conteo).

### ⚠️ Interacción con N4 (insignia desbloqueada)

`ScoringService.NotifyNewAchievementsAsync` notifica "¡Insignia desbloqueada!" para **cualquier** clave nueva de `GetForPoolAsync` al capturar un resultado. Sin exclusión, la primera vez que alguien fuera mejor/peor del día recibiría esa notificación **a media tarde** (con el día aún abierto y el resultado aún reversible), duplicando y adelantando la N10 de las 9:30 PM. Se **excluyen** las dos claves nuevas de N4:

```csharp
// ScoringService.NotifyNewAchievementsAsync
foreach (var ach in achievements.Where(a =>
    a.Badge.Key is not "daily-best" and not "daily-worst"
    && !alreadyNotified.Contains((userId, a.Badge.Key))))
```

La comunicación de estas dos insignias es exclusiva de N10.

### Criterios de aceptación

- [x] Las 2 insignias aparecen en el catálogo (celda + bottom sheet) con su categoría/color
- [x] Se muestran obtenidas (a color) si el conteo es ≥ 1, en gris si es 0
- [x] Debajo del nombre aparece una 🏅 por cada día ganado/sufrido, con wrap
- [x] El conteo refleja toda la historia de la sala (backfill implícito por ser on-demand)
- [x] N4 no envía "¡Insignia desbloqueada!" para `daily-best`/`daily-worst`
- [x] Las 11 insignias existentes se ven exactamente igual que antes

---

## Módulo M4 — Tarjeta "El peor del día" en Resumen diario (agregado 2026-07-08)

### Objetivo

El Resumen diario (`/pools/{poolId}/daily-summary`) ya muestra la tarjeta "🏆 El mejor del día" (`DayLeaders`, Módulo L). Se agrega debajo la tarjeta **"🥴 El peor del día"** con los miembros que menos puntos sumaron ese día, navegable día por día igual que el resto del resumen. (El usuario pidió esto para "el módulo de evolución"; ver la nota de interpretación del encabezado — la vista día-por-día es el Resumen diario.)

### Reglas de la tarjeta

- Participan **todos los miembros** de la sala: sin pronóstico ese día = 0 puntos (mismo criterio que `DailyAwardService`).
- Se muestra solo si el día tiene diferencias (`max != min` sobre todos los miembros); si todos empatan no se muestra (igual que hoy la de mejor cuando nadie sumó).
- Es **informativa, no premia**: con empate en el mínimo muestra a todos los empatados con el título en plural ("Los peores del día") aunque ese día la medalla 🥴 no se otorgue a nadie. La tarjeta informa el hecho; la medalla (M2/M3) exige peor único.
- La tarjeta de "El mejor del día" existente **no cambia** (sigue mostrando empatados y ocultándose si `maxPoints == 0`).

### Implementación

- `DailySummaryService.DailySummary` gana el campo `List<DayLeader> DayWorst` (reusa el record `DayLeader`). El cálculo reusa el `totalsByUser` del paso 7 (predicciones del día + campeón el día de la Final), completado con 0 para miembros sin filas; `DayWorst` = miembros con el mínimo, solo si `max != min`. De paso, la lista de miembros (`memberList`, con `DisplayName`/`ProfilePicturePath`) se trae una sola vez y la reusa también la tarjeta de mejores (antes hacía una segunda consulta a `PoolMembers`).
- `DailySummary.razor`: tarjeta nueva después de "El mejor del día", reusando las clases `ds-best-*` con el modificador `ds-worst`; los puntos van en rojo (`.ds-worst-pts`, `var(--q-red)`) y se muestran como `0 pts` cuando el peor no sumó.

### Criterios de aceptación

- [x] La tarjeta aparece debajo de "El mejor del día" en cada día con diferencias de puntos
- [x] Miembro sin pronósticos aparece como peor con `0 pts`
- [x] Con empate en el mínimo, el título dice "Los peores del día" y lista a todos los empatados
- [x] Día con todos los miembros empatados: la tarjeta no se muestra
- [x] La tarjeta de "El mejor del día" se ve exactamente igual que antes

---

## Módulo N10 — Notificación "mejor/peor del día" (9:30 PM CDMX)

*Extiende la serie N0–N9 de `07_notificaciones.md`.*

### Objetivo

Todos los días **a las 21:30 hora CDMX**, en cada sala con premio del día (ver M2): el mejor recibe su festejo, el peor su burla, y el resto de la sala el anuncio con nombres. El link de las tres lleva al módulo de logros: `/pools/{poolId}/achievements`.

**Depende de M2** (el cálculo) y llega **antes** del resumen diario N9 de las 22:00 — primero el chisme, luego las cuentas.

### Mensajes (textos elegidos por el usuario)

Personal — mejor del día:

> **🔮 Hoy amaneciste brujo**
> Fuiste el mejor del día en "{sala}".
> Nadie te llegó ni a los talones.
> Pasa a recoger tu medalla 🏅

Personal — peor del día:

> **🥴 Ouch… el peor del día**
> Nadie pronosticó peor que tú hoy en "{sala}".
> Una moneda al aire lo hace mejor.
> Medalla de plomo a tu vitrina 🏅

Anuncio — resto de la sala:

> **📰 Última hora en "{sala}"**
> {mejor} es el mejor del día 👑
> {peor}… mejor ni preguntes 💀

Con la regla de ganador único (ajuste 2026-07-08) ya no hay versión con empates: cada lado tiene a lo más un nombre. Si un lado quedó empatado (sin medalla), **su línea se omite del anuncio** y nadie recibe la versión personal de ese lado — p. ej. con mejor único y mínimo empatado, el mejor recibe su festejo y el resto solo "{mejor} es el mejor del día 👑". Nombres = `User.DisplayName`.

### Implementación

Nuevo método `CheckDailyAwardsAsync(db, now)` en `NotificationCheckService`, registrado en `CheckAndNotifyAsync` **antes** de `CheckDailySummaryAsync` (N9). Lo dispara el mismo ping de la Azure Function cada 10 min — en la práctica llega entre 21:30 y ~21:40.

```csharp
private const string DailyAwardType = "DailyAward";

private async Task CheckDailyAwardsAsync(QuinielaDbContext db, DateTime now)
{
    var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, Tz);
    if (localNow.Hour < 21 || (localNow.Hour == 21 && localNow.Minute < 30)) return;
    // ...
}
```

Lógica (calca la estructura de N9 — `CheckDailySummaryAsync`):

1. **¿Hay jornada?** Partidos `Finalizado` con `KickoffUtc` dentro del día local actual. Si no hay: return. `MatchId` de log = último partido finalizado del día.
2. **Dedup:** `NotificationLog` con `Type == "DailyAward"` cuyo `Match.KickoffUtc` cae en el mismo día local (join a `Match`, igual que N9) → ese usuario ya fue procesado hoy, omitir. **Una fila por usuario y día**, sin importar cuántas salas/variantes recibió: todas sus salas se envían en la misma corrida antes de registrar el log (mismo patrón que N9; `NotificationLog` no tiene `PoolId`).
3. **Por cada sala:** `DailyAwardService.GetForDayAsync(poolId, día)`. Si devuelve null (ninguna medalla ese día — todos empatados o ambos lados empatados): la sala se salta — **sus miembros no reciben nada de N10 ese día**.
4. **Por cada miembro** de la sala con premio: si es `BestUserId` → personal de mejor; si es `WorstUserId` → personal de peor; si no → anuncio con las líneas de los lados que sí otorgaron medalla (el lado empatado se omite). Un usuario nunca es ambos (`max == min` ya descartó ese caso). URL: `/pools/{poolId}/achievements`.
5. Registrar el `NotificationLog` por usuario y `SaveChangesAsync`.

Notas:

- Es **evento de sala**: incluye nombre de sala y un usuario en 2 salas puede recibir 2 notificaciones (p. ej. brujo en una, anuncio en la otra).
- La verificación del conteo de medallas que pide el flujo ("calcular/verificar al finalizar el día antes de enviar") está implícita: N10 usa el mismo `DailyAwardService` on-demand que la vitrina — no hay estado que sincronizar.

### Limitaciones aceptadas (corte fijo 21:30, mismo principio que N9)

- Resultados capturados **después** de las 21:30 no entran en la notificación de ese día y no provocan reenvío ni retractación; la vitrina (on-demand) sí se auto-corrige. La notificación es la invitación, la vitrina es la verdad.
- Si la app estuviera dormida sin pings todo el tramo 21:30–23:59, el premio de ese día no se anuncia (las medallas sí quedan, por ser on-demand).
- N10 (21:30) + N9 (22:00) son dos pushes en media hora los días de jornada. Se acepta por ahora (contenido distinto: chisme vs. cuentas); si resulta ruidoso, la mejora futura es fusionarlos en uno — decisión reversible, anotada igual que la relación N3/N9.

### Criterios de aceptación

- [x] Llega entre 21:30 y ~21:40 CDMX, una sola vez al día por usuario, solo en días con ≥ 1 partido finalizado
- [x] El mejor recibe "🔮 Hoy amaneciste brujo"; el peor "🥴 Ouch… el peor del día"; el resto "📰 Última hora" con nombres
- [x] Lado con empate: nadie recibe la versión personal de ese lado y su línea se omite del anuncio (el otro lado se anuncia normal)
- [x] Día sin ninguna medalla (todos empatados o ambos lados empatados): nadie de esa sala recibe N10
- [x] El link abre `/pools/{poolId}/achievements` y las medallas del día ya se ven en la vitrina
- [x] Usuarios sin suscripción activa se omiten silenciosamente (garantizado por `PushNotificationService`)
