using System.Security.Claims;
using Monica.Authority.Identity.Abstractions;
using Monica.Tool.Extensions;

namespace Monica.Authority.Identity.Services;

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