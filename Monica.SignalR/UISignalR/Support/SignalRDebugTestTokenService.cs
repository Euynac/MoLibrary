using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Authority.Authentication.Abstractions;
using Monica.Authority.Identity.Models;
using Monica.Modules;

namespace Monica.SignalR.UISignalR.Support;

/// <summary>
/// A freshly minted SignalR debug test-user token.
/// </summary>
/// <param name="Username">The random username the token was issued for.</param>
/// <param name="AccessToken">The signed JWT access token.</param>
public sealed record SignalRDebugTestToken(string Username, string AccessToken);

/// <summary>
/// Mints random test-user access tokens for the SignalR debug page.
/// </summary>
/// <remarks>
/// The minted token is a fully valid JWT for a fresh random user, so the capability is environment-gated: it is
/// enabled by default only when the host runs in the Development environment and the Monica authentication module
/// is registered. Hosts can force the behavior through <see cref="ModuleSignalRUIOption.EnableTestUserConnection"/>.
/// </remarks>
public sealed class SignalRDebugTestTokenService(
    IServiceProvider serviceProvider,
    IOptions<ModuleSignalRUIOption> options,
    IHostEnvironment hostEnvironment)
{
    private const string TEST_USERNAME_PREFIX = "signalr-debug-";
    private const string TEST_USER_NICKNAME = "SignalR Debug";

    /// <summary>
    /// Gets a value indicating whether a test-user token can be minted in the current host.
    /// </summary>
    public bool IsEnabled
    {
        get
        {
            var optionValue = options.Value.EnableTestUserConnection;
            return (optionValue ?? hostEnvironment.IsDevelopment())
                   && serviceProvider.GetService<IAccessTokenIssuer>() is not null;
        }
    }

    /// <summary>
    /// Mints an access token for a fresh random test user.
    /// </summary>
    /// <returns>The token together with the username it was issued for.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the capability is disabled or the authentication module is not registered.
    /// </exception>
    public SignalRDebugTestToken GenerateTestUserToken()
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException(
                "Test-user token generation is unavailable. It requires the Development environment (or an explicit "
                + $"{nameof(ModuleSignalRUIOption.EnableTestUserConnection)} override) and the Monica authentication module.");
        }

        var issuer = serviceProvider.GetRequiredService<IAccessTokenIssuer>();
        var username = TEST_USERNAME_PREFIX + Guid.NewGuid().ToString("N")[..8];

        // Mirror SystemUserManager's claim shape so the connection resolves a normal (non-system) CurrentUser.
        var claims = new Claim[]
        {
            new(AuthorityClaimTypes.Username, username),
            new(AuthorityClaimTypes.UserId, Guid.NewGuid().ToString()),
            new(AuthorityClaimTypes.Nickname, TEST_USER_NICKNAME)
        };

        return new SignalRDebugTestToken(username, issuer.GenerateTokens(username, claims, DateTime.Now));
    }
}
