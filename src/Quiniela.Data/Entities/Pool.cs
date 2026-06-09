namespace Quiniela.Data.Entities;

public class Pool
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string JoinCode { get; set; }
    public int OwnerId { get; set; }
    public int PtsCorrect { get; set; } = 3;
    public int PtsBonusKO { get; set; } = 2;
    public DateTime CreatedAt { get; set; }

    public User Owner { get; set; } = null!;
    public ICollection<PoolMember> Members { get; set; } = [];
    public ICollection<Prediction> Predictions { get; set; } = [];
}
