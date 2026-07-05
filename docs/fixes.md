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
