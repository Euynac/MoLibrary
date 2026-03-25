using System.Security.Claims;

namespace Monica.Authority.Identity.Abstractions;

public interface ICurrentUserBase
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