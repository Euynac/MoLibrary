using System.Security.Claims;

namespace Monica.Authority.Authentication.Abstractions;

public interface IMoAuthManager
{
    string GenerateTokens(string username, Claim[] claims, DateTime? now = null);
}