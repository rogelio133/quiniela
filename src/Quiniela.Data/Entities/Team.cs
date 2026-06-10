namespace Quiniela.Data.Entities;

public class Team
{
    public int Id { get; set; }
    public required string Name { get; set; }
    // ISO 3166-1 alpha-2 in lowercase (e.g. "mx", "us") — used by flag-icons CSS: fi fi-{FlagCode}
    public required string FlagCode { get; set; }
    // 2-3 letter display code shown in the match card (e.g. "MX", "ENG", "WAL") — needed because FlagCode for some nations is compound ("gb-eng")
    public string? ShortCode { get; set; }
    public char? GroupCode { get; set; }

    public ICollection<Match> HomeMatches { get; set; } = [];
    public ICollection<Match> AwayMatches { get; set; } = [];
}
