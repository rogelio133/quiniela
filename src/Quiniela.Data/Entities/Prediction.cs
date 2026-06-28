namespace Quiniela.Data.Entities;

public class Prediction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PoolId { get; set; }
    public int MatchId { get; set; }
    public char PredOutcome { get; set; }  // 'H' = Local, 'D' = Empate, 'A' = Visitante
    public MatchDecidedIn? PredInstance { get; set; }  // Instancia pronosticada. NULL en grupos.
    public int Points { get; set; }
    public int PtsResult { get; set; }    // Puntos por acertar resultado/avance (desglose de Points)
    public int PtsInstance { get; set; }  // Puntos por acertar instancia KO (desglose de Points)
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Pool Pool { get; set; } = null!;
    public Match Match { get; set; } = null!;
}
