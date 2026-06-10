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

    public Team? HomeTeam { get; set; }
    public Team? AwayTeam { get; set; }
    public ICollection<Prediction> Predictions { get; set; } = [];
}
