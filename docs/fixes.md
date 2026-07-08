# Fixes

## Fix 1 — Botón "Ver pronósticos de todos" en partidos en curso ✅

**Pantalla:** Pronósticos  
**Descripción:** En la pantalla de pronósticos, si un partido ya está en curso, mostrar el botón de "Ver pronósticos de todos".

**Comportamiento esperado:**  
- Cuando un partido tiene estado "en curso" (live/in progress), el botón para ver los pronósticos de todos los participantes debe ser visible, igual que cuando el partido ya terminó.

**Comportamiento actual:**  
- El botón solo se muestra cuando el partido ha finalizado; si el partido está en curso, el botón no aparece.

## Fix 2 — Error "An item with the same key has already been added" en Standings ✅

**Pantalla:** Standings (`/pools/{poolId}/standings`)  
**Descripción:** Al corregir el resultado de un partido ya finalizado (ej. un partido de octavos), la pantalla de standings tronaba con `ArgumentException: An item with the same key has already been added. Key: 7`.

**Causa raíz:**  
- `ScoringService.SaveSnapshotAsync` (`src/Quiniela.Web/Services/ScoringService.cs`) inserta filas nuevas en `StandingsSnapshots` cada vez que se recalculan los puntos de un partido (`RecalculateForMatchAsync`), pero nunca borraba los snapshots insertados en una corrida anterior para ese mismo `matchId`.
- Al corregir el resultado del partido, `RecalculateForMatchAsync` se ejecutó una segunda vez, generando snapshots duplicados por usuario para el mismo `(PoolId, MatchId, UserId)`.
- `StandingsService.GetLastSnapshotPositionsAsync` arma un diccionario con `ToDictionaryAsync(s => s.UserId, ...)` sobre esos snapshots, y al haber dos filas con el mismo `UserId` para el mismo partido, la construcción del diccionario fallaba.

**Fix:**  
- En `SaveSnapshotAsync`, antes de insertar los snapshots nuevos, se borran (`ExecuteDeleteAsync`) los snapshots existentes del mismo `matchId` para los pools afectados.
- Adicionalmente, fue necesario limpiar en producción las filas duplicadas ya insertadas en `StandingsSnapshots` (dejando la más reciente por `SavedAt`/`Id` de cada trío `PoolId, MatchId, UserId`), ya que el fix de código solo previene duplicados futuros.

**Query de limpieza (SQL Server):**
```sql
;WITH Ranked AS (
    SELECT Id,
           ROW_NUMBER() OVER (
               PARTITION BY PoolId, MatchId, UserId
               ORDER BY SavedAt DESC, Id DESC
           ) AS rn
    FROM StandingsSnapshots
)
DELETE FROM StandingsSnapshots
WHERE Id IN (SELECT Id FROM Ranked WHERE rn > 1);
```

## Fix 3 — Evolución de posiciones: la línea sale plana para todos los jugadores ✅

**Pantalla:** Evolución de posiciones (`/pools/{poolId}/standings/history`)

**Descripción:**
En la sala real "Mundial 133" (4 jugadores, ~7 partidos con snapshot), la gráfica muestra una línea **perfectamente horizontal** para los 4 jugadores — Fabian Pacheco `#1 → #1 —`, abraham ruiz `#2 → #2 —`, Rogelio 515 `#3 → #3 —`, Abraham Dario Pacheco Elorza `#4 → #4 —` — sin ninguna variación visible entre puntos. La gráfica no comunica ninguna evolución.

Esto es un problema distinto (y posterior) al ya corregido en esta misma sesión: antes de ese fix, los puntos `<circle>` dentro del `viewBox` no-uniforme (`preserveAspectRatio="none"`) se veían como manchas/elipses fusionadas formando un patrón ondulado ilegible. Ese fix (puntos renderizados como `<div>` posicionados por `%` en vez de `<circle>` en el SVG) ya se aplicó y confirmadamente corrige el renderizado — pero una vez que los puntos se ven correctamente, revela que los valores reales detrás de la gráfica son constantes. Es decir: el patrón ondulado que se veía originalmente **no representaba movimiento real de posición** — era artefacto visual puro (distorsión de la elipse, posiblemente agravado por el animation-delay escalonado de `popIn` capturado a medio animar). El dato real, una vez visible correctamente, es plano.

**Causa raíz (hipótesis con respaldo en código — pendiente de confirmar contra los datos reales de `StandingsSnapshots` de "Mundial 133", a los que no tengo acceso desde este entorno porque esa sala vive en la base de producción, no en `pc133\sqlexpress`):**

- `ScoringService.SaveSnapshotAsync` ([ScoringService.cs:64-94](../src/Quiniela.Web/Services/ScoringService.cs#L64-L94)) calcula la posición de **ese** snapshot llamando a `standingsService.GetStandingsAsync(poolId)` ([StandingsService.cs:21-63](../src/Quiniela.Web/Services/StandingsService.cs#L21-L63)), que suma `Predictions.Points` de **todas** las predicciones del pool sin filtrar por fecha de partido. En otras palabras: siempre calcula "el standings de HOY", nunca "el standings tal como estaba justo después de este partido en particular".
- Esto es inofensivo la primera vez que un partido se finaliza en estricto orden cronológico (en ese momento "hoy" coincide con "hasta este partido", porque ningún partido posterior tiene puntos todavía).
- Pero `AdminService.SaveResultAsync` ([AdminService.cs:38-67](../src/Quiniela.Web/Services/AdminService.cs#L38-L67)) llama a `RecalculateForMatchAsync` (y por lo tanto a `SaveSnapshotAsync`) tanto al capturar un resultado por primera vez **como al corregir uno ya finalizado** — el mismo flujo "Corregir resultado" que motivó el Fix 2 de este documento, y que sabemos que se usa activamente. Cada vez que se corrige **cualquier** partido, aunque sea de una fase temprana, su snapshot se vuelve a calcular con el standings **actual** (que ya incluye los puntos de todos los partidos finalizados desde entonces, no solo los que existían cronológicamente hasta ese partido).
- Con varias correcciones acumuladas a lo largo del tiempo (algo plausible en ~3-4 semanas de torneo), cada una "reescribe" el snapshot del partido corregido con la posición casi-final del momento de la corrección. Si suficientes snapshots terminan siendo recalculados así, todos convergen hacia prácticamente la misma posición final — la línea se aplana, perdiendo cualquier variación real que haya existido en su momento.
- Este bug es independiente del bug de renderizado ya corregido: ese fix solo garantiza que los puntos se vean como círculos limpios; no cambia (ni podía cambiar) los valores de `Position` ya almacenados en `StandingsSnapshot`.

**Cómo confirmarlo:** consultar `StandingsSnapshots` de la sala real, ordenado por `Match.KickoffUtc`, comparando `SavedAt` contra el `KickoffUtc` del propio partido — si hay snapshots de partidos de kickoff temprano cuyo `SavedAt` es mucho más tardío (evidencia de que fueron recalculados por una corrección posterior, no en el momento original), eso confirmaría la hipótesis. No se pudo hacer esta verificación en esta sesión porque "Mundial 133" no existe en la base local (`pc133\sqlexpress` solo tiene la sala de prueba "Quiniela casita", sin historial suficiente) — vive en el entorno de producción.

**Fix implementado (2026-07-04) — alcance elegido por el usuario: cascada completa:**

- `StandingsService.GetStandingsAsync` ahora tiene una sobrecarga `GetStandingsAsync(QuinielaDbContext db, int poolId, DateTime? asOfKickoffUtc)` que acota los `Predictions` sumados a `Match.KickoffUtc <= asOfKickoffUtc` (si es `null`, comportamiento idéntico al de siempre: "standings de hoy"). Los puntos de campeón (`ChampionPrediction`) solo se incluyen si el partido `Final` ya tuvo su `KickoffUtc` para esa fecha de corte (nunca se resuelven antes de la Final, así que no pueden "filtrarse" hacia atrás en snapshots tempranos). El overload de un solo argumento (`GetStandingsAsync(int poolId)`, usado por Standings/Index, PlayerStatsService, Achievements, etc.) delega en este con `asOfKickoffUtc: null`, sin cambios de comportamiento para esos llamadores.
- `ScoringService.SaveSnapshotAsync` ahora, por cada pool afectado por el partido que se está recalculando: (1) obtiene el `KickoffUtc` de ese partido, (2) busca todos los `matchId` que ya tengan snapshot en ese pool con `KickoffUtc >= ` el del partido corregido (más el propio partido, por si es la primera vez que se snapshotea), (3) borra esos snapshots existentes, y (4) los reconstruye uno por uno llamando a `GetStandingsAsync(db, poolId, kickoffDeEseMatch)` — es decir, cada snapshot vuelve a reflejar el standings *tal como era hasta ese partido*, ya con los puntos corregidos. Esto resuelve tanto el caso normal (primer snapshot de un partido, acotado correctamente en vez de "a hoy") como el caso de corrección retroactiva (los snapshots de partidos posteriores ya guardados se recalculan en cascada, no solo el del partido corregido).
- **Backfill de snapshots ya existentes (2026-07-04, agregado a pedido del usuario):** el punto anterior dejaba los snapshots viejos de producción sin corregir hasta la próxima finalización/corrección. Se agregó `DbInitializer.BackfillStandingsSnapshotsAsync`, llamado al final de `SeedAsync` (mismo patrón idempotente que `BackfillBracketOrderAsync`/`BackfillPredictionPointsAsync`: corre en cada arranque de la app, solo escribe si algún valor difiere del recalculado, no-op silencioso si ya está todo correcto). Recorre cada `(PoolId, MatchId)` que ya tenga snapshot, y para cada uno recalcula el standings acotado al `KickoffUtc` de ese partido (misma fórmula que `StandingsService.GetStandingsAsync(db, poolId, asOfKickoffUtc)`, reimplementada aquí porque `Quiniela.Data` no depende de `Quiniela.Web` — mismo motivo por el que `BackfillPredictionPointsAsync` ya reimplementaba la lógica de `ScoringService` en vez de invocarlo). No crea snapshots nuevos para partidos que nunca tuvieron uno (mismo criterio de "sin backfill retroactivo de historial" del Módulo F) — solo corrige los que ya existen.
- **Cómo se aplica:** no requiere ninguna acción manual en la base de datos. Se ejecuta solo, automáticamente, la próxima vez que la app arranque (el próximo deploy en producción, o `dotnet run` en local) — igual que ya pasa con los otros dos backfills del proyecto. Al terminar, loggea `"Backfilled {N} standings snapshot rows..."` si corrigió algo, o nada si ya estaba todo bien.
- **Verificación:** `dotnet build` limpio (0 errores). Verificado end-to-end contra la BD local (`pc133\sqlexpress`, sala "Quiniela casita"): (1) corrida normal sin cambios pendientes → no reescribe nada (0 filas, sin log); (2) se corrompió a mano una fila de `StandingsSnapshots` (Points/Position con valores incorrectos a propósito, simulando el estado real de producción) y se reinició la app → el log mostró `"Backfilled 1 standings snapshot rows..."` y la fila quedó exactamente con los valores correctos originales. No se corrió contra la base real "Mundial 133" en esta sesión — se aplicará solo en el próximo deploy a producción.

## Fix 4 — Resumen diario "secuestra" la navegación: al salir hacia otra página, regresa solo al resumen ✅

**Pantalla:** Resumen diario (`/pools/{poolId}/daily-summary`)

**Descripción:**
Estando en el resumen diario, después de navegar entre fechas (botones ◀/▶ o el selector de fecha), al hacer clic en cualquier link para salir de la pantalla — el breadcrumb al detalle de la sala (`/pools/{id}`), "Mis Salas", o cualquier otro — la navegación "deja de funcionar": la página destino aparece un instante (o ni eso) y la app regresa sola al resumen diario. El usuario queda atrapado en el módulo.

**Comportamiento esperado:**
Los links salen normalmente de la pantalla; el resumen diario no debe re-navegar hacia sí mismo cuando el usuario ya va de salida.

**Causa raíz (encadenamiento de 3 factores en [DailySummary.razor](../src/Quiniela.Web/Components/Pages/Pools/DailySummary.razor)):**

1. **`[SupplyParameterFromQuery]` escucha *todos* los cambios de URL, no solo los de su propia ruta.** La app usa el router estático (en [Routes.razor](../src/Quiniela.Web/Components/Routes.razor) no hay render mode interactivo global) con interactividad por página, así que los links `<a href>` navegan vía *enhanced navigation*. Desde .NET 8, un componente interactivo con `[SupplyParameterFromQuery]` ([DailySummary.razor:181](../src/Quiniela.Web/Components/Pages/Pools/DailySummary.razor#L181)) queda suscrito a `NavigationManager.LocationChanged` a través del `SupplyParameterFromQueryValueProvider`, y ese evento se dispara con **cualquier** navegación — incluida la que sale de la página. Además, el aviso de cambio de URL llega al circuito **antes** de que el componente viejo sea desechado (el dispose ocurre hasta que el SSR de la página destino llega y se parcha el DOM).

2. **Al salir, el componente todavía vivo recibe `Date = null` y lo interpreta como "cambio de fecha".** El clic a `/pools/{id}` (URL sin `?date=`) re-ejecuta `OnParametersSetAsync` ([DailySummary.razor:215-242](../src/Quiniela.Web/Components/Pages/Pools/DailySummary.razor#L215-L242)) con `Date = null`. Como para entonces `lastLoadedDate` ya tiene valor (p. ej. `"2026-06-14"`), el guard `Date == lastLoadedDate` no aplica y el componente recarga el resumen del día más reciente.

3. **La "normalización de URL" re-navega hacia el resumen.** Al terminar esa recarga, la condición `Date != lastLoadedDate` ([DailySummary.razor:240-241](../src/Quiniela.Web/Components/Pages/Pools/DailySummary.razor#L240-L241)) es verdadera (`null` vs la fecha cargada) y se ejecuta `NavigateToDay(...)` → `NavigationManager.NavigateTo("/pools/{id}/daily-summary?date=...", replace: true)`. Es decir: el componente, que debía estar muriendo, **navega de regreso al resumen diario** justo cuando el usuario iba llegando a la página destino. El `replace: true` agrava el daño: sobrescribe la entrada de historial de la página destino, así que ni el botón "atrás" del navegador rescata al usuario.

**Por qué se manifiesta "después de navegar por las fechas":** la re-navegación del punto 3 solo dispara si el circuito interactivo ya tiene `hasLoadedOnce = true` y un `lastLoadedDate` distinto de `null` — exactamente el estado en que queda el componente tras usar ◀/▶ o el selector. (En teoría también puede reproducirse entrando y saliendo sin tocar fechas, porque la normalización inicial de la URL también deja `lastLoadedDate` poblado, pero el flujo de navegar fechas lo garantiza siempre.)

**Solución implementada (2026-07-08 — dos guards en `DailySummary.razor`, sin tocar servicios ni el patrón de URL `?date=`):**

1. **Ignorar los updates "fantasma" cuando la URL ya no es la de esta página.** Al inicio de `OnParametersSetAsync`, si el path actual ya no corresponde a la ruta del componente, salir sin recargar ni navegar:

   ```csharp
   private bool IsOnThisPage()
   {
       var path = new Uri(NavigationManager.Uri).AbsolutePath;
       return path.Equals($"/pools/{PoolId}/daily-summary", StringComparison.OrdinalIgnoreCase);
   }

   protected override async Task OnParametersSetAsync()
   {
       if (!IsOnThisPage()) return; // el usuario va de salida: no recargar ni re-navegar

       // ... lógica actual ...

       summary = await DailySummaryService.GetAsync(PoolId, currentUserId, requested);

       // ... y re-verificar DESPUÉS del await, antes de normalizar la URL:
       if (_disposed || !IsOnThisPage()) return;
       if (summary is not null && Date != lastLoadedDate)
           NavigateToDay(summary.Date);
   }
   ```

2. **Nunca navegar desde un componente ya desechado (defensa en profundidad).** Implementar `IDisposable` con una bandera `_disposed` y verificarla después del `await` (como en el snippet de arriba). El `await DailySummaryService.GetAsync(...)` puede tardar lo suficiente para que el componente sea desechado a media corrida; sin esta bandera, la continuación ejecuta el `NavigateTo` póstumo.

   ```csharp
   @implements IDisposable

   private bool _disposed;
   public void Dispose() => _disposed = true;
   ```

   El re-chequeo de `IsOnThisPage()` post-`await` cubre además la ventana en la que la URL ya cambió pero el dispose aún no llega.

**Verificación (2026-07-08, ejecutada — navegador real vía Playwright contra la BD local, login admin, sala 1, que ya tiene varios días de resultados gracias al backfill del Fix 5):** entrar al resumen diario sin `?date=` normaliza la URL al día más reciente (`?date=2026-07-04`); ◀ retrocede 3 fechas actualizando la URL correctamente en cada paso; después de navegar fechas: (a) clic en el breadcrumb al detalle de la sala → se queda en `/pools/1`, (b) clic en "Mis Salas" → se queda en `/pools`, (c) botón atrás del navegador → se queda en la página previa — en los tres casos esperando 2.5s extra para dar tiempo a cualquier re-navegación "fantasma", sin rebote al resumen y sin errores de consola. Un `?date=1999-01-01` inválido pegado a mano cae al día más reciente y normaliza la URL. Todos los checks en verde (`RESULT: ALL PASS`).

## Fix 5 — Evolución de posiciones y resumen diario no contemplan la fase de grupos ✅

**Pantallas:** Evolución de posiciones (`/pools/{poolId}/standings/history`) y Resumen diario (`/pools/{poolId}/daily-summary`)

**Descripción:**
En ambos módulos, la información arranca recién en las eliminatorias: la gráfica de evolución no tiene ningún punto de la fase de grupos, y en el resumen diario los días de fase de grupos no muestran la posición del jugador ni su movimiento (aunque los partidos y puntos del día sí aparecen).

**Causa raíz:**
Ambos módulos leen `StandingsSnapshots` — la evolución vía `StandingsService.GetPositionHistoryAsync` (un punto por snapshot) y el resumen diario vía `DailySummaryService.SnapshotPositionAsync` (último snapshot anterior al corte del día, para `Position`/`PreviousPosition`). Los snapshots se crean en `ScoringService.SaveSnapshotAsync` **al finalizar un partido**, pero ese mecanismo (Módulo F) se deployó a media fase de grupos/inicio de eliminatorias: todos los partidos de grupos ya estaban finalizados para entonces, así que nunca generaron snapshot. El backfill agregado en el Fix 3 (`DbInitializer.BackfillStandingsSnapshotsAsync`) deliberadamente solo **corregía** snapshots existentes, nunca **creaba** faltantes ("sin backfill retroactivo de historial del Módulo F") — criterio que este fix revierte.

No hace falta una migración de esquema: es puramente un backfill de datos, y el proyecto ya tiene el mecanismo idempotente para eso (los `Backfill*` de `DbInitializer.SeedAsync` que corren en cada arranque).

**Fix (2026-07-08):**

- **`DbInitializer.BackfillStandingsSnapshotsAsync` ahora también crea los snapshots faltantes.** Al conjunto de `(PoolId, MatchId)` con snapshot existente se le une el conjunto *esperado*: todo partido `Finalizado` con al menos una predicción en el pool — el mismo criterio con el que `ScoringService.SaveSnapshotAsync` decide qué pools snapshotear en vivo (esto excluye naturalmente salas creadas después de un partido: no pueden tener predicciones sobre él). Para cada clave, se calcula el standings acotado al `KickoffUtc` del partido (lógica ya existente del Fix 3) y: las filas existentes se corrigen si difieren (comportamiento previo), y los miembros sin fila reciben una nueva (`SavedAt = ahora`). Sigue siendo idempotente: segunda corrida sin cambios = 0 escrituras, sin log.
- **`StandingsService.GetLastSnapshotPositionsAsync` ahora identifica el "último snapshot" por `Match.KickoffUtc` (desc, con `MatchId` como desempate) en vez de por `SavedAt`.** Necesario porque las filas backfilleadas comparten un `SavedAt` de hoy — con el orden viejo, la pantalla de Standings habría tomado un partido de grupos arbitrario como "snapshot más reciente" para las flechas de movimiento. De paso corrige un problema latente del Fix 3: la cascada de `SaveSnapshotAsync` también reescribe muchos snapshots con el mismo `SavedAt`, haciendo ambiguo ese orden. (Los partidos simultáneos comparten corte de standings, así que el desempate por `MatchId` es inocuo.)

**Cómo se aplica:** igual que los demás backfills — sin acción manual; corre solo en el próximo arranque de la app (próximo deploy en producción). Loggea `"Backfilled standings snapshots: {N} rows corrected, {M} rows created."` si escribió algo.

**Verificación:** `dotnet build` limpio. End-to-end contra la BD local (`pc133\sqlexpress`, `QuinielaDB`), que reproduce la forma del problema de producción (72 partidos de grupos finalizados con 0 snapshots; pool 1 con predicciones en 14 de ellos + 1 de dieciseisavos ya con snapshot): (1) primer arranque → log `"0 rows corrected, 28 rows created"` (14 partidos × 2 miembros); (2) las filas creadas tienen puntos acumulados correctos por corte de kickoff (usuario 1: 3→6→9→14; usuario 2: 3→…→12; totales finales idénticos a `SUM(Predictions.Points)`) y posiciones que evolucionan (usuario 1: #1 → #2 a media fase de grupos → #1 en dieciseisavos); (3) el snapshot preexistente del partido de dieciseisavos quedó intacto (`SavedAt` original); (4) segundo arranque → 0 escrituras, sin log (idempotente). No se corrió contra la base real de producción en esta sesión — se aplicará solo en el próximo deploy.
