namespace Quiniela.Data.Entities;

public class NotifiedAchievement
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PoolId { get; set; }
    public string AchievementKey { get; set; } = "";
    public DateTime NotifiedAt { get; set; }

    public User User { get; set; } = null!;
    public Pool Pool { get; set; } = null!;
}
