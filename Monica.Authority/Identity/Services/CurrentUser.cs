using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Monica.Authority.Identity.Abstractions;
using Monica.Authority.Identity.Models;

namespace Monica.Authority.Identity.Services;

public class CurrentUser : CurrentUserBase, ICurrentUser
{
    // Important: specify the constructor when multiple overloads exist
    //https://stackoverflow.com/a/57016321
    [ActivatorUtilitiesConstructor]
    public CurrentUser(ICurrentPrincipalAccessor principalAccessor): base(principalAccessor.Principal)
    {
        
    }

    public CurrentUser(ClaimsPrincipal principal) : base(principal)
    {
    }

    public virtual string? RoleId => FindClaimValue(AuthorityClaimTypes.RoleId);

    public virtual string? Nickname => FindClaimValue(AuthorityClaimTypes.Nickname);


    public virtual string? Username => FindClaimValue(AuthorityClaimTypes.Username);

    public virtual string? Id => FindClaimValue(AuthorityClaimTypes.UserId);
}
