namespace Quiniela.Data.Entities;

public class ChampionPrediction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PoolId { get; set; }
    public int TeamId { get; set; }
    public int Points { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Pool Pool { get; set; } = null!;
    public Team Team { get; set; } = null!;
}
