using System.Security.Claims;

namespace Monica.Authority.Identity.Abstractions;

public interface ICurrentUserBase
{
    /// <summary>
    /// Indicates whether the user is authenticated
    /// </summary>
    bool IsAuthenticated { get; }
    /// <summary>
    /// Claims information for the current user
    /// </summary>
    ClaimsPrincipal ClaimsPrincipal { get; }

    Claim? FindClaim(string claimType);

    Claim[] FindClaims(string claimType);

    Claim[] GetAllClaims();
    string? FindClaimValue(string claimType);
    T FindClaimValue<T>(string claimType) where T : struct;
}
