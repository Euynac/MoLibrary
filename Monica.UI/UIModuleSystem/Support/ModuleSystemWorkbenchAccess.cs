using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Modules;

namespace Monica.UI.UIModuleSystem.Support;

/// <summary>
/// Evaluates the secure diagnostics boundary before the page invokes Core or the shell exposes its navigation item.
/// </summary>
public sealed class ModuleSystemWorkbenchAccess(
    IHostEnvironment environment,
    IOptions<ModuleSystemUIOption> options,
    IServiceProvider services)
{
    /// <summary>Returns whether the current circuit may inspect the module diagnostics facade.</summary>
    public async Task<bool> IsAuthorizedAsync()
    {
        if (environment.IsDevelopment())
        {
            return true;
        }

        var configured = options.Value;
        var policyName = configured.AuthorizationPolicy;
        if (!configured.EnableOutsideDevelopment || string.IsNullOrWhiteSpace(policyName))
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

        // Resolve the named policy explicitly so a host typo denies access instead of throwing from authorization.
        try
        {
            var policy = await policyProvider.GetPolicyAsync(policyName);
            if (policy is null)
            {
                return false;
            }

            var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
            var authorization = await authorizationService.AuthorizeAsync(
                authenticationState.User,
                resource: null,
                policy.Requirements);
            return authorization.Succeeded;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
