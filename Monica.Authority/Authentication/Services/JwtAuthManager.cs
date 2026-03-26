using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Monica.Authority.Authentication.Abstractions;
using Monica.Authority.Authentication.Models;
using Monica.Authority.Authorization.Exceptions;
using Monica.Authority.Identity.Extensions;
using Monica.Modules;

namespace Monica.Authority.Authentication.Services;

public class JwtAuthManager(IOptions<ModuleAuthenticationOption> jwtTokenConfig) : IJwtAuthManager, IAccessTokenIssuer
{
    protected ModuleAuthenticationOption JwtTokenConfig => jwtTokenConfig.Value;
    public IImmutableDictionary<string, RefreshToken> UsersRefreshTokensReadOnlyDictionary => _usersRefreshTokens.ToImmutableDictionary();
    private readonly ConcurrentDictionary<string, RefreshToken> _usersRefreshTokens = new();  // can store in a database or a distributed cache

    // optional: clean up expired refresh tokens
    public void RemoveExpiredRefreshTokens(DateTime now)
    {
        var expiredTokens = _usersRefreshTokens.Where(x => x.Value.ExpireAt < now).ToList();
        foreach (var expiredToken in expiredTokens)
        {
            _usersRefreshTokens.TryRemove(expiredToken.Key, out _);
        }
    }

    // can be more specific to ip, user agent, device name, etc.
    public void RemoveRefreshTokenByUsername(string username)
    {
        var refreshTokens = _usersRefreshTokens.Where(x => x.Value.Username == username).ToList();
        foreach (var refreshToken in refreshTokens)
        {
            _usersRefreshTokens.TryRemove(refreshToken.Key, out _);
        }
    }

    public JwtAuthResult GenerateTokens(string username, Claim[] claims, DateTime? now = null)
    {
        now ??= DateTime.Now;
        var shouldAddAudienceClaim = string.IsNullOrWhiteSpace(claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Aud)?.Value);
        var accessExpiresAt = now.Value.AddMinutes(JwtTokenConfig.AccessTokenExpiration);
        var refreshExpiresAt = now.Value.AddMinutes(JwtTokenConfig.RefreshTokenExpiration);
        var jwtToken = new JwtSecurityToken(
            JwtTokenConfig.Issuer,
            shouldAddAudienceClaim ? JwtTokenConfig.Audience : string.Empty,
            claims,
            expires: accessExpiresAt,
            signingCredentials: new SigningCredentials(JwtTokenConfig.SecurityKey, SecurityAlgorithms.HmacSha256Signature));
        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwtToken);

        var refreshToken = new RefreshToken
        {
            Username = username,
            TokenString = GenerateRefreshTokenString(),
            ExpireAt = refreshExpiresAt
        };
        _usersRefreshTokens.AddOrUpdate(refreshToken.TokenString, refreshToken, (_, _) => refreshToken);

        return new JwtAuthResult
        {
            AccessToken = accessToken,
            ExpiresAt = accessExpiresAt,
            RefreshTokenObj = refreshToken
        };
    }

    public JwtAuthResult Refresh(string refreshToken, string accessToken, DateTime? now = null)
    {
        now ??= DateTime.Now;
        var (principal, jwtToken) = DecodeJwtToken(accessToken);
        if (jwtToken == null || !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256Signature))
        {
            throw new SecurityTokenException("Invalid token");
        }

        var username = principal.AsCurrentUser().Username;
        if (!_usersRefreshTokens.TryGetValue(refreshToken, out var existingRefreshToken))
        {
            throw new AuthorizationException(AuthorizationException.ExceptionType.RefreshTokenExpired);
        }
        if (existingRefreshToken.Username != username || existingRefreshToken.ExpireAt < now)
        {
            throw new AuthorizationException(AuthorizationException.ExceptionType.RefreshTokenExpired);
        }

        return GenerateTokens(username, principal.Claims.ToArray(), now); // need to recover the original claims
    }

    public (ClaimsPrincipal, JwtSecurityToken?) DecodeJwtToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new SecurityTokenException("Invalid token");
        }

        if (token.StartsWith("bearer", StringComparison.OrdinalIgnoreCase))
        {
            token = token[6..].TrimStart();
        }

        var principal = new JwtSecurityTokenHandler()
            .ValidateToken(token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = JwtTokenConfig.Issuer,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtTokenConfig.SecurityKey,
                    ValidAudience = JwtTokenConfig.Audience,
                    ValidateAudience = true,
                    ValidateLifetime = false, // Skip token lifetime validation
                    ClockSkew = TimeSpan.FromMinutes(1),
                },
                out var validatedToken);
        return (principal, validatedToken as JwtSecurityToken);
    }

    private static string GenerateRefreshTokenString()
    {
        var randomNumber = new byte[32];
        using var randomNumberGenerator = RandomNumberGenerator.Create();
        randomNumberGenerator.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    string IAccessTokenIssuer.GenerateTokens(string username, Claim[] claims, DateTime? now)
    {
        return GenerateTokens(username, claims, now).AccessToken;
    }
}
