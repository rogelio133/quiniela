using Microsoft.AspNetCore.Identity;

namespace Quiniela.Data.Entities;

public class User : IdentityUser<int>
{
    public required string DisplayName { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Pool> OwnedPools { get; set; } = [];
    public ICollection<PoolMember> PoolMemberships { get; set; } = [];
    public ICollection<Prediction> Predictions { get; set; } = [];
}
