using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Monica.Authority.Identity.Abstractions;
using Monica.Authority.Identity.Models;

namespace Monica.Authority.Identity.Services;

public class MoCurrentUser : MoCurrentUserBase, IMoCurrentUser
{
    //巨坑：当多个构造函数时，需要指定Constructor
    //https://stackoverflow.com/a/57016321
    [ActivatorUtilitiesConstructor]
    public MoCurrentUser(IMoCurrentPrincipalAccessor principalAccessor): base(principalAccessor.Principal)
    {
        
    }

    public MoCurrentUser(ClaimsPrincipal principal) : base(principal)
    {
    }

    public virtual string? RoleId => FindClaimValue(MoClaimTypes.RoleId);

    public virtual string? Nickname => FindClaimValue(MoClaimTypes.Nickname);


    public virtual string? Username => FindClaimValue(MoClaimTypes.Username);

    public virtual string? Id => FindClaimValue(MoClaimTypes.UserId);
}