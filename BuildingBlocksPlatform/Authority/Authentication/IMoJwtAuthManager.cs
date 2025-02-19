using System.Collections.Immutable;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BuildingBlocksPlatform.Authority.Authentication;

public interface IMoJwtAuthManager
{
    IImmutableDictionary<string, RefreshToken> UsersRefreshTokensReadOnlyDictionary { get; }
    JwtAuthResult GenerateTokens(string username, Claim[] claims, DateTime now);
    JwtAuthResult Refresh(string refreshToken, string accessToken, DateTime now);
    void RemoveExpiredRefreshTokens(DateTime now);
    void RemoveRefreshTokenByUsername(string username);
    (ClaimsPrincipal, JwtSecurityToken?) DecodeJwtToken(string token);
}