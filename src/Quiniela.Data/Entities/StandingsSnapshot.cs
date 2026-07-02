namespace Quiniela.Data.Entities;

public class StandingsSnapshot
{
    public int Id { get; set; }
    public int PoolId { get; set; }
    public int MatchId { get; set; }
    public int UserId { get; set; }
    public int Position { get; set; }
    public int Points { get; set; }
    public DateTime SavedAt { get; set; }

    public Pool Pool { get; set; } = null!;
    public User User { get; set; } = null!;
    public Match Match { get; set; } = null!;
}
