using System.Security.Claims;

namespace Monica.Authority.Authentication.Abstractions;

public interface IAccessTokenIssuer
{
    string GenerateTokens(string username, Claim[] claims, DateTime? now = null);
}