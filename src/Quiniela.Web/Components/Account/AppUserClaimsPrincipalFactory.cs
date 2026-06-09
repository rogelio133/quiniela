using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Quiniela.Data.Entities;

namespace Quiniela.Web.Components.Account;

public sealed class AppUserClaimsPrincipalFactory(
    UserManager<User> userManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("DisplayName", user.DisplayName));
        return identity;
    }
}
