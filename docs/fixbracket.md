# Fix: Errores en módulo de Brackets
## Archivo: fixbracket.md

> Mundial 2026 · Diagnóstico y plan de corrección para dos errores reportados
> en el módulo de brackets/eliminatorias.

---

## Error 1 — Orden visual de Octavos no corresponde con Dieciseisavos

### Síntoma

En `/bracket` (`Components/Pages/Bracket/Index.razor`), los partidos de
Octavos no aparecen alineados debajo de los dos partidos de 16avos que
realmente los alimentan (el ganador de cada 16avo no queda visualmente
conectado con el Octavo correcto).

### Causa raíz

El `BracketOrder` (columna que determina la posición visual de cada partido
dentro de su ronda) se calcula de dos formas **inconsistentes** según la fase:

1. **Octavos → Final**: `DbInitializer.ComputeBracketOrders` (líneas
   308-329) recorre el árbol de eliminación de arriba hacia abajo —
   Final → Semifinal → Cuartos — usando las referencias `"Ganador/Perdedor
   Partido NN"` que aparecen en el campo `nota` de `matches.json`. Esto
   produce un `BracketOrder` geométricamente correcto para Cuartos, Semifinal,
   TercerLugar y Final, y también para **Octavos** (porque cada partido de
   Cuartos referencia sus dos Octavos de origen con `"Partido NN"`).

2. **Dieciseisavos**: `SeedDieciseisavosAsync` (líneas 237-278) asigna el
   `BracketOrder` simplemente como `i + 1` tras ordenar los partidos **por
   fecha/hora de kickoff** (`OrderBy(p => p.Fecha_Utc)`). Este orden no tiene
   ninguna relación con el árbol de eliminación.

El recorrido de `ComputeBracketOrders` **nunca llega a Dieciseisavos**: el
`chain` (línea 316) solo incluye `[Final, Semifinal, Cuartos]`, es decir,
se detiene un nivel antes de necesitar el vínculo Octavos → Dieciseisavos.
Y aunque se agregara Octavos a ese `chain`, el formato de las notas de
Octavos es distinto al de las rondas superiores: en vez de `"Ganador Partido
NN"`, usa **nombres de equipo entre paréntesis** cuando el cruce aún no está
definido, ej.:

```json
"nota": "Ganador (Portugal vs. Croacia) vs. Ganador (España vs. Austria)"
```

("Portugal vs. Croacia" es exactamente el partido de 16avos que produce a
ese equipo — no un número de partido FIFA). Cuando el cruce **ya** está
definido (ej. partido 89: Paraguay vs. Francia), ni siquiera hay `nota`: solo
se sabe el nombre del equipo ganador, no de qué partido de 16avos salió.

En resumen: falta el nivel de traducción Octavos → Dieciseisavos, y ese nivel
requiere una estrategia distinta a la de los niveles superiores (matching por
nombre de equipo, no por número de partido).

### Impacto

- Solo afecta la **visualización** del bracket (`/bracket`). No afecta
  puntajes, pronósticos ni ninguna otra pantalla.
- Afecta únicamente el límite Dieciseisavos↔Octavos; Cuartos, Semifinal,
  TercerLugar y Final ya están correctamente enlazados entre sí.

### Opciones de solución

**Opción A (recomendada) — Extender `ComputeBracketOrders` con un paso de
matching por nombre de equipo:**

1. Cargar también `mundial2026_dieciseisavos.json` (ya se hace en
   `SeedDieciseisavosAsync`) y construir un diccionario
   `equipo → Match_Id de 16avos` (cada uno de los 32 equipos aparece
   exactamente una vez, como local o visitante, en ese archivo).
2. Para cada partido de Octavos (ya ordenado 1-8 por el paso existente):
   - Si tiene `equipo_local`/`equipo_visitante` definidos: usar esos nombres
     directamente contra el diccionario para hallar sus dos 16avos de origen.
   - Si tiene `nota` (cruce aún no definido): tomar el primer equipo de cada
     grupo entre paréntesis (ej. "Portugal" y "España") — cualquiera de los
     dos equipos de cada 16avo sirve para identificar el partido, ya que
     ambos pertenecen al mismo `Match_Id`.
   - Asignar `BracketOrder = 2*pos-1` al 16avo del lado local y `2*pos` al
     del lado visitante (donde `pos` es el `BracketOrder` ya calculado del
     Octavo).
3. Usar ese resultado en dos lugares:
   - `SeedDieciseisavosAsync`: reemplazar el `OrderBy(Fecha_Utc)` + `i+1`
     por el orden calculado (requiere reordenar `SeedAsync` para calcular
     los órdenes **antes** de sembrar, ya que hoy Dieciseisavos se siembra
     antes que Octavos).
   - `BackfillBracketOrderAsync`: extender el `Where` y el diccionario
     `orderByStageAndKickoff` para incluir también `MatchStage.Dieciseisavos`,
     de forma que la BD ya sembrada (ambiente actual del usuario) se corrija
     sin necesidad de re-sembrar desde cero.

**Opción B — Hardcodear el `BracketOrder` de Dieciseisavos:** dado que los
16avos ya están 100% definidos (no son placeholders), se podría simplemente
escribir a mano el orden correcto (16 valores) directamente en el JSON o en
un diccionario estático. Más rápido de implementar, pero frágil si el torneo
cambia el `matches.json` de Octavos más adelante (habría que volver a
mapear a mano).

**Recomendación:** Opción A — reutiliza el patrón ya existente
(`ComputeBracketOrders` + `BackfillBracketOrderAsync`), es la solución
generalizable si en el futuro se re-siembra o corrige `matches.json`.

### Archivos a modificar

| Archivo | Cambio |
|---|---|
| `src/Quiniela.Data/Seeding/DbInitializer.cs` | Extender `ComputeBracketOrders` (o agregar método hermano) con el paso de matching Octavos→Dieciseisavos por nombre de equipo; actualizar `SeedDieciseisavosAsync` y `BackfillBracketOrderAsync` |

---

## Error 2 — "Ver pronósticos de todos" no muestra información calculada en partidos finalizados

### Síntoma

Al tocar "Ver pronósticos de todos" en un partido finalizado
(`MatchCard.razor` → `MatchPredictionsSheet.razor`), el modal abre y lista a
los jugadores, pero para varios partidos muestra **0 pts y "✗" para todos**,
aunque esos jugadores sí acertaron (se ve reflejado correctamente en otras
pantallas como Standings).

### Causa raíz

Confirmado directamente en la base de datos (`QuinielaDB`, tabla
`Predictions`):

```sql
SELECT m.Stage, COUNT(*) AS Total,
       SUM(CASE WHEN p.Points > 0 AND p.PtsResult = 0 AND p.PtsInstance = 0
                THEN 1 ELSE 0 END) AS Mismatched
FROM Predictions p JOIN Matches m ON m.Id = p.MatchId
WHERE m.Status = 1
GROUP BY m.Stage
```

Resultado: **7 de 18** predicciones de grupo ya finalizadas tienen
`Points > 0` pero `PtsResult = 0` y `PtsInstance = 0`.

Explicación: las columnas `PtsResult`/`PtsInstance` se agregaron en la
migración `AddPredictionPointsBreakdown` (Módulo 8, 2026-06-28) para separar
el puntaje de "acierto de resultado" del "acierto de instancia" (KO). Antes
de esa fecha, `ScoringService.RecalculateForMatchAsync` solo calculaba el
campo `Points` (total). Los partidos de grupo finalizados **antes** de esa
migración nunca volvieron a pasar por `RecalculateForMatchAsync` después del
cambio, así que sus `Predictions` quedaron con `Points` correcto (histórico)
pero `PtsResult`/`PtsInstance` en su valor por defecto (`0`).

`MatchPredictionsService.GetForMatchAsync` (líneas 50-64) y
`MatchPredictionsSheet.razor` construyen **toda** la vista a partir de
`PtsResult`/`PtsInstance`/`Points` (siendo `Points` una propiedad calculada
`PtsResult + PtsInstance`, no el campo real de la entidad) — nunca leen el
campo `Prediction.Points` original. Por eso el modal muestra 0/✗ para esas
predicciones antiguas, mientras que `Standings`/`MyPredictions` (que sí usan
`Prediction.Points` directamente) muestran el puntaje correcto.

### Impacto

- Solo afecta al modal "Ver pronósticos de todos" (`MatchPredictionsSheet`).
- Afecta a **todos los partidos finalizados antes del 2026-06-28** que no
  hayan sido corregidos después (mayormente partidos de grupo). Los KO
  finalizados en adelante ya se calculan bien porque siempre pasan por el
  `ScoringService` post-migración.

### Solución

1. **Corrección hacia adelante:** no requiere cambio de código — desde el
   Módulo 8, cada `SaveResultAsync` (nuevo o corrección) ya invoca
   `RecalculateForMatchAsync`, que sí llena `PtsResult`/`PtsInstance`
   correctamente.

2. **Backfill retroactivo (confirmado con el usuario):** ejecutar
   `ScoringService.RecalculateForMatchAsync(matchId)` para cada partido con
   `Status = Finalizado` que actualmente existe en la BD. Es idempotente y
   ya recibe el `matchId` como parámetro, así que puede reutilizarse tal cual
   — solo hace falta un punto de entrada que itere todos los partidos
   finalizados una sola vez:
   - Opción más simple: un pequeño método en `DbInitializer` (o un
     comando/endpoint temporal de un solo uso) que haga
     `foreach (var m in db.Matches.Where(m => m.Status == Finalizado))
        await scoringService.RecalculateForMatchAsync(m.Id);`
   - Alternativa: correrlo manualmente una vez vía un script/consola,
     sin dejar código permanente en el repo.
   - **Nota:** `RecalculateForMatchAsync` también recalcula y guarda un
     `StandingsSnapshot` por cada corrida (`SaveSnapshotAsync`). Si se
     re-ejecuta para los 18+ partidos históricos, se van a generar snapshots
     adicionales "fuera de tiempo" (todos con `SavedAt = ahora`). Hay que
     decidir si eso es aceptable o si conviene hacer el backfill de
     `PtsResult`/`PtsInstance` con una consulta SQL directa (UPDATE) en vez
     de pasar por `ScoringService`, para no ensuciar el historial de
     posiciones. **Pendiente de decidir en la implementación.**

### Archivos a modificar / crear

| Archivo | Cambio |
|---|---|
| `src/Quiniela.Web/Services/ScoringService.cs` | Ninguno (la lógica ya es correcta) |
| Script/método de backfill (nuevo, un solo uso) | Recalcular `PtsResult`/`PtsInstance` de partidos ya finalizados |

---

## Error 3 — Tab "Bracket" del admin no muestra venue ni fecha del partido

### Síntoma

En `/admin`, tab **Bracket** (asignación manual de equipos a partidos de
eliminatorias sin definir), cada tarjeta de partido solo muestra la fase
(`StageLabel(match)`) y un badge estático con el texto **"PLACEHOLDER"**. No
se ve la sede (venue) ni la fecha/hora del encuentro, así que el admin no
tiene forma de saber, a simple vista, a qué partido específico corresponde
cada tarjeta cuando hay varios placeholders de la misma fase.

### Causa raíz

`Components/Pages/Admin/Index.razor`, tab Bracket (líneas 395-452): el
`card-header` (líneas 398-403) solo renderiza `StageLabel(match)` y el badge
`PLACEHOLDER` — nunca lee `match.Venue` ni `match.KickoffUtc`, a pesar de que
`placeholderMatches` (poblado por `KnockoutService.GetPlaceholderMatchesAsync`)
ya trae esos campos cargados en cada `Match` (no requiere una query
adicional, es puramente un problema de plantilla/render).

`KnockoutStageView.razor` (usado en `/fases`) ya resuelve este mismo
problema para la vista de jugador: tiene un helper `FormatKickoff` que
convierte `KickoffUtc` a `America/Mexico_City` y lo formatea como
`"dd MMM · HH:mm"` en `es-MX`, y muestra `match.Venue` en un `<div
class="ko-venue">`. El mismo patrón puede reutilizarse en el admin.

### Impacto

- Solo afecta la usabilidad del tab Bracket del admin — no hay pérdida de
  datos ni error funcional, solo falta de contexto visual al asignar
  equipos.

### Solución propuesta

En el `card-header` de cada tarjeta placeholder (`Admin/Index.razor`,
~línea 398-403):

1. Mantener `StageLabel(match)`.
2. Reemplazar o complementar el badge `PLACEHOLDER` con la fecha/hora local
   del partido (mismo formato y conversión de zona horaria que
   `KnockoutStageView.FormatKickoff`, `America/Mexico_City`).
3. Agregar el `Venue` del partido (si no es null/vacío) — se puede reusar el
   mismo patrón de `ko-venue` de `KnockoutStageView.razor`, o simplemente
   agregarlo como una segunda línea de texto pequeño dentro del
   `card-header`/`card-body`.

Ejemplo de estructura final del header:

```
OCTAVOS                              04 JUL · 21:00
Philadelphia Stadium (Lincoln Financial Field)
```

### Archivos a modificar

| Archivo | Cambio |
|---|---|
| `src/Quiniela.Web/Components/Pages/Admin/Index.razor` | Mostrar `Venue` + `KickoffUtc` (convertido a hora local) en el header de cada tarjeta placeholder del tab Bracket, reemplazando/complementando el badge "PLACEHOLDER" |

---

## Resumen

| # | Hecho | Error | Alcance | Fix propuesto |
|---|---|---|---|---|
| 1 | [x] | Orden visual Octavos vs. 16avos en `/bracket` | Solo visualización | Extender `ComputeBracketOrders` con matching por nombre de equipo (Opción A) + actualizar `BackfillBracketOrderAsync` |
| 2 | [x] | "Ver pronósticos de todos" no calcula bien en finalizados | Modal `MatchPredictionsSheet`, partidos finalizados antes del 2026-06-28 | Backfill retroactivo de `PtsResult`/`PtsInstance` para partidos ya finalizados |
| 3 | [x] | Tab Bracket del admin no muestra venue/fecha | Solo visualización, `/admin` tab Bracket | Mostrar `Venue` + `KickoffUtc` local en el header de cada tarjeta placeholder |

Los tres fixes de este documento ya están implementados.
