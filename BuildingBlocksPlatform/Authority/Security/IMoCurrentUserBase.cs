using BuildingBlocksPlatform.Authority.Implements.Security;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace BuildingBlocksPlatform.Authority.Security;

public interface IMoCurrentUser : IMoCurrentUserBase
{
    /// <summary>
    /// 用户Id
    /// </summary>
    string? Id { get; }
    /// <summary>
    /// 角色Id
    /// </summary>
    string? RoleId { get; }
    /// <summary>
    /// 用户昵称
    /// </summary>
    string? Nickname { get; }
    /// <summary>
    /// 当前用户登录名
    /// </summary>
    string? Username { get; }
}

public interface IMoCurrentUserBase
{
    /// <summary>
    /// 是否认证成功
    /// </summary>
    bool IsAuthenticated { get; }
    /// <summary>
    /// 当前用户Claims信息
    /// </summary>
    ClaimsPrincipal ClaimsPrincipal { get; }

    Claim? FindClaim(string claimType);

    Claim[] FindClaims(string claimType);

    Claim[] GetAllClaims();
    string? FindClaimValue(string claimType);
    T FindClaimValue<T>(string claimType) where T : struct;
}

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


public abstract class MoCurrentUserBase(ClaimsPrincipal principal) : IMoCurrentUserBase
{
    private static readonly Claim[] _emptyClaimsArray = [];
    public ClaimsPrincipal ClaimsPrincipal { get; } = principal;

    public virtual bool IsAuthenticated => ClaimsPrincipal.Identity?.IsAuthenticated is true;
    public virtual Claim? FindClaim(string claimType)
    {
        return ClaimsPrincipal.Claims.FirstOrDefault(c => c.Type == claimType);
    }

    public virtual Claim[] FindClaims(string claimType)
    {
        return ClaimsPrincipal.Claims.Where(c => c.Type == claimType).ToArray() ?? _emptyClaimsArray;
    }

    public virtual Claim[] GetAllClaims()
    {
        return ClaimsPrincipal.Claims.ToArray() ?? _emptyClaimsArray;
    }

    public string? FindClaimValue(string claimType)
    {
        return FindClaim(claimType)?.Value;
    }

    public T FindClaimValue<T>(string claimType) where T : struct
    {
        var claimValue = FindClaimValue(claimType);
        if (claimValue == null) return default;

        return claimValue.To<T>();
    }
}