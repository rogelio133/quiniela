namespace Quiniela.Data.Entities;

public class PageVisitLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int PoolId { get; set; }
    public Pool Pool { get; set; } = null!;
    public string PageName { get; set; } = null!;   // nombre amigable del catálogo, ej. "Tabla de posiciones"
    public string Url { get; set; } = null!;        // relativa, ej. "pools/1/standings"
    public DateTime VisitedAt { get; set; }         // UTC
}
