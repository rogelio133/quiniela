namespace Quiniela.Data.Entities;

public class Jugador
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public required string Nombre { get; set; }
    public required string Posicion { get; set; }

    public Team Team { get; set; } = null!;
}
