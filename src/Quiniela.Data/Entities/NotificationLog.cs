namespace Quiniela.Data.Entities;

public class NotificationLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    // Null en anuncios globales (no ligados a un partido), p. ej. Type = "Announcement:*"
    public int? MatchId { get; set; }
    public string Type { get; set; } = "";
    public DateTime SentAt { get; set; }

    public User User { get; set; } = null!;
    public Match? Match { get; set; }
}
