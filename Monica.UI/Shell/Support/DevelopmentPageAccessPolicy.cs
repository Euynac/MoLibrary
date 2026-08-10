using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Monica.UI.Shell.Support;

/// <summary>
/// Defines options for a diagnostics page that is available in Development and explicitly policy-gated elsewhere.
/// </summary>
public interface IDevelopmentPageAccessOptions
{
    /// <summary>
    /// Gets whether the page may be considered for access outside the Development environment.
    /// </summary>
    bool EnableOutsideDevelopment { get; }

    /// <summary>
    /// Gets the named host authorization policy required outside Development.
    /// </summary>
    string? AuthorizationPolicy { get; }
}

/// <summary>
/// Implements the common fail-closed access boundary for operational pages that are Development-only by default.
/// </summary>
/// <typeparam name="TOptions">The owning UI module's access options.</typeparam>
public abstract class DevelopmentPageAccessPolicy<TOptions>(
    IHostEnvironment environment,
    IOptions<TOptions> options,
    IServiceProvider services) : IPageAccessPolicy
    where TOptions : class, IDevelopmentPageAccessOptions
{
    /// <inheritdoc />
    public async Task<bool> IsAuthorizedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (environment.IsDevelopment())
        {
            return true;
        }

        var configured = options.Value;
        if (!configured.EnableOutsideDevelopment || string.IsNullOrWhiteSpace(configured.AuthorizationPolicy))
        {
            return false;
        }

        var authenticationStateProvider = services.GetService<AuthenticationStateProvider>();
        var authorizationService = services.GetService<IAuthorizationService>();
        var policyProvider = services.GetService<IAuthorizationPolicyProvider>();
        if (authenticationStateProvider is null || authorizationService is null || policyProvider is null)
        {
            return false;
        }

        try
        {
            var policy = await policyProvider.GetPolicyAsync(configured.AuthorizationPolicy);
            cancellationToken.ThrowIfCancellationRequested();
            if (policy is null)
            {
                return false;
            }

            var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var authorization = await authorizationService.AuthorizeAsync(
                authenticationState.User,
                resource: null,
                policy.Requirements);
            cancellationToken.ThrowIfCancellationRequested();
            return authorization.Succeeded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
