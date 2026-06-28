namespace Quiniela.Data.Entities;

public enum MatchStage
{
    Grupos = 0,
    Dieciseisavos = 1,
    Octavos = 2,
    Cuartos = 3,
    Semifinal = 4,
    TercerLugar = 5,
    Final = 6
}

public enum MatchStatus
{
    Programado = 0,
    Finalizado = 1
}

public enum MatchDecidedIn
{
    Regular90 = 0,
    ExtraTime = 1,
    Penalties = 2
}

public class Match
{
    public int Id { get; set; }
    public int? HomeTeamId { get; set; }
    public int? AwayTeamId { get; set; }
    public DateTime KickoffUtc { get; set; }
    public string? Venue { get; set; }
    public MatchStage Stage { get; set; }
    public char? GroupCode { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public MatchStatus Status { get; set; }

    // Instancia a la que llegó el partido. NULL en grupos y en KO no finalizados.
    public MatchDecidedIn? DecidedIn { get; set; }

    // Etiquetas del cruce cuando aún no se conocen los equipos (bracket placeholder).
    public string? HomeSlotLabel { get; set; }
    public string? AwaySlotLabel { get; set; }

    // Orden del partido dentro de su fase (1..16 en dieciseisavos), para el bracket.
    public int? BracketOrder { get; set; }

    public Team? HomeTeam { get; set; }
    public Team? AwayTeam { get; set; }
    public ICollection<Prediction> Predictions { get; set; } = [];
}
