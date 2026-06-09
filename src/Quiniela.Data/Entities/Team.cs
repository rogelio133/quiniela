namespace Quiniela.Data.Entities;

public class Team
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public char? GroupCode { get; set; }

    public ICollection<Match> HomeMatches { get; set; } = [];
    public ICollection<Match> AwayMatches { get; set; } = [];
}
