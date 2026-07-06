namespace Quiniela.Data.Entities;

public class PushSubscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";   // clave pública del browser
    public string Auth { get; set; } = "";     // secreto del browser
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
