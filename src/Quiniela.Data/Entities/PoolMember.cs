namespace Quiniela.Data.Entities;

public class PoolMember
{
    public int PoolId { get; set; }
    public int UserId { get; set; }

    public Pool Pool { get; set; } = null!;
    public User User { get; set; } = null!;
}
