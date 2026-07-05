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
- **No se hizo backfill** de los snapshots ya existentes en producción ("Mundial 133") que fueron guardados con la lógica vieja (standings "a hoy", sin acotar) — igual que la decisión ya tomada en el Módulo F, este fix solo garantiza corrección desde ahora en adelante. Los snapshots viejos seguirán planos hasta que se corrija o finalice algún partido, momento en el que la cascada los recalculará correctamente.
- **Verificación:** `dotnet build` limpio (0 errores). No se verificó en navegador contra datos reales porque requeriría corregir un resultado en la sala real "Mundial 133" (o insertar snapshots sintéticos), y ambas cosas mutarían datos de producción que usan los amigos activamente — mismo criterio de no tocar esa base que en sesiones anteriores. Queda pendiente confirmar visualmente la próxima vez que se corrija un resultado real.
