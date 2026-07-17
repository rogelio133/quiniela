using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiniela.Data.Migrations
{
    /// <summary>
    /// Backfill de PredictionHistories para la fase de grupos (ver docs/backfillgrupos.md).
    /// La tabla se creó cuando la fase de grupos ya había terminado, así que los pronósticos
    /// de grupos no tienen historial. Reconstruye un historial aproximado desde Predictions:
    /// una fila con CreatedAt (pronóstico original) y, si UpdatedAt difiere, otra con
    /// UpdatedAt (última edición). Ambas llevan el PredOutcome actual (el original se perdió)
    /// y PredInstance = NULL (en grupos nunca aplica instancia).
    /// Idempotente: omite pronósticos que ya tengan cualquier fila de historial.
    /// </summary>
    public partial class BackfillGroupStagePredictionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vacío: no hay marca que distinga las filas del backfill de las
            // orgánicas, así que revertir borraría historial legítimo (docs/backfillgrupos.md).
        }
    }
}
