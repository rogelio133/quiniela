namespace Quiniela.Data.Entities;

public class NotificationLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int MatchId { get; set; }
    public string Type { get; set; } = "";
    public DateTime SentAt { get; set; }

    public User User { get; set; } = null!;
    public Match Match { get; set; } = null!;
}
