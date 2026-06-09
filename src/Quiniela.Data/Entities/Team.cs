namespace Quiniela.Data.Entities;

public class Team
{
    public int Id { get; set; }
    public required string Name { get; set; }
    // ISO 3166-1 alpha-2 in lowercase (e.g. "mx", "us") — used by flag-icons CSS: fi fi-{FlagCode}
    public required string FlagCode { get; set; }
    public char? GroupCode { get; set; }

    public ICollection<Match> HomeMatches { get; set; } = [];
    public ICollection<Match> AwayMatches { get; set; } = [];
}
