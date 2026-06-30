namespace Quiniela.Data.Entities;

public class HistorialMundial
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public required string Mundial { get; set; }
    public required string Posicion { get; set; }

    public Team Team { get; set; } = null!;
}
