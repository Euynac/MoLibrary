using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using BuildingBlocksPlatform.Authority.Implements.Authorization;
using BuildingBlocksPlatform.Authority.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocksPlatform.Authority.Authentication;

public class MoJwtAuthManager(IOptions<MoJwtTokenOptions> jwtTokenConfig) : IMoJwtAuthManager, IMoAuthManager
{
    protected MoJwtTokenOptions JwtTokenConfig => jwtTokenConfig.Value;
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

        var username = principal.AsMoCurrentUser().Username;
        if (!_usersRefreshTokens.TryGetValue(refreshToken, out var existingRefreshToken))
        {
            throw new MoAuthorizationException(MoAuthorizationException.ExceptionType.RefreshTokenExpired);
        }
        if (existingRefreshToken.Username != username || existingRefreshToken.ExpireAt < now)
        {
            throw new MoAuthorizationException(MoAuthorizationException.ExceptionType.RefreshTokenExpired);
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
                    ValidateLifetime = false,//不校验Token有效期
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

    string IMoAuthManager.GenerateTokens(string username, Claim[] claims, DateTime? now)
    {
        return GenerateTokens(username, claims, now).AccessToken;
    }
}

public class JwtAuthResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken => RefreshTokenObj.TokenString;

    /// <summary>
    /// Token类型
    /// </summary>
    public string TokenType => "bearer";
    /// <summary>
    /// AccessToken失效时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    [JsonIgnore]
    public RefreshToken RefreshTokenObj { get; set; } = new();
}

public class RefreshToken
{
    public string Username { get; set; } = string.Empty;    // can be used for usage tracking
    // can optionally include other metadata, such as user agent, ip address, device name, and so on

    public string TokenString { get; set; } = string.Empty;

    public DateTime ExpireAt { get; set; }
}