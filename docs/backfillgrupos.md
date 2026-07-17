# Backfill de PredictionHistories — Fase de Grupos

## Contexto

La tabla `PredictionHistories` se creó con la migración `20260705161441_AddPredictionHistory`
(5 de julio de 2026). Para entonces la fase de grupos ya había terminado: los 18 pronósticos
de grupos se crearon/editaron entre el 9 y el 24 de junio, por lo que **ninguno tiene
historial**. Este backfill reconstruye un historial aproximado a partir de los datos que sí
sobrevivieron en `Predictions` (`PredOutcome`, `CreatedAt`, `UpdatedAt`).

## Reglas del backfill

Solo aplica a pronósticos de partidos con `Matches.Stage = 0` (Grupos) que **no tengan ya**
filas en `PredictionHistories` (garantiza idempotencia):

1. Insertar un registro con `ChangedAt = Predictions.CreatedAt` — representa el pronóstico
   original.
2. Si `Predictions.UpdatedAt <> Predictions.CreatedAt`, insertar un segundo registro con
   `ChangedAt = Predictions.UpdatedAt` — representa la última edición.

En ambos casos:

- `PredOutcome` = `Predictions.PredOutcome` (el valor actual; ver limitaciones).
- `PredInstance` = `NULL` (en grupos nunca aplica instancia; el código de
  `PredictionService.UpsertAsync` siempre guarda `NULL` para esta fase).

## Limitaciones conocidas (aceptadas)

- **El resultado original se perdió.** Solo se conserva el `PredOutcome` final, así que la
  fila 1 (la de `CreatedAt`) lleva el valor actual, que puede no ser lo que el usuario
  eligió originalmente. Las dos filas de un pronóstico editado tendrán el mismo outcome.
- **`UpdatedAt` puede moverse sin cambio real.** `UpsertAsync` actualiza `UpdatedAt` en cada
  guardado aunque el outcome no cambie (`PredictionService.cs`, rama de edición), por lo que
  `CreatedAt <> UpdatedAt` no garantiza que hubo un cambio de resultado — la fila 2 puede
  ser un "re-guardado" idéntico. No hay forma de distinguirlo con los datos disponibles.
- **Los cambios intermedios no se recuperan.** Si hubo más de una edición, solo queda rastro
  de la última fecha.

## Números esperados (BD dev, verificado 2026-07-17)

| Concepto | Cantidad |
|---|---|
| Pronósticos de grupos sin historial | 18 |
| De ellos, con `UpdatedAt <> CreatedAt` | 4 |
| **Filas a insertar** | **22** (18 + 4) |

## Script SQL

Idempotente: si un pronóstico ya tiene cualquier fila de historial, se omite completo.

```sql
INSERT INTO PredictionHistories (PredictionId, PredOutcome, PredInstance, ChangedAt)
SELECT p.Id, p.PredOutcome, NULL, x.ChangedAt
FROM Predictions p
JOIN Matches m ON m.Id = p.MatchId
CROSS APPLY (
    SELECT p.CreatedAt AS ChangedAt
    UNION ALL
    SELECT p.UpdatedAt WHERE p.UpdatedAt <> p.CreatedAt
) x
WHERE m.Stage = 0  -- MatchStage.Grupos
  AND NOT EXISTS (
      SELECT 1 FROM PredictionHistories ph WHERE ph.PredictionId = p.Id
  );
```

## Aplicación como migración EF (recomendado)

Para que el backfill viaje con el pipeline normal de despliegue:

```bash
dotnet ef migrations add BackfillGroupStagePredictionHistory --project src/Quiniela.Data --startup-project src/Quiniela.Web
```

Y en el `Up()` de la migración generada, ejecutar el script anterior con
`migrationBuilder.Sql(...)`. El `Down()` puede quedar vacío o eliminar las filas insertadas;
como no hay marca que distinga las filas del backfill de las orgánicas, se recomienda
dejar `Down()` vacío y documentarlo.

## Verificación posterior

```sql
-- Debe regresar 0 (ningún pronóstico de grupos sin historial)
SELECT COUNT(*)
FROM Predictions p
JOIN Matches m ON m.Id = p.MatchId
WHERE m.Stage = 0
  AND NOT EXISTS (SELECT 1 FROM PredictionHistories ph WHERE ph.PredictionId = p.Id);

-- Conteo de filas por fase (Stage 0 debe mostrar 22)
SELECT m.Stage, COUNT(ph.Id) AS HistRows
FROM PredictionHistories ph
JOIN Predictions p ON p.Id = ph.PredictionId
JOIN Matches m ON m.Id = p.MatchId
GROUP BY m.Stage ORDER BY m.Stage;
```


