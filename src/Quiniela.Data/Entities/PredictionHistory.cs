namespace Quiniela.Data.Entities;

public class PredictionHistory
{
    public int Id { get; set; }
    public int PredictionId { get; set; }
    public char PredOutcome { get; set; }
    public MatchDecidedIn? PredInstance { get; set; }
    public DateTime ChangedAt { get; set; }

    public Prediction Prediction { get; set; } = null!;
}
