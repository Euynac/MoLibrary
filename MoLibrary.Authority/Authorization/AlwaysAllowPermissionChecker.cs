using System.Security.Claims;

namespace MoLibrary.Authority.Authorization;

/// <summary>
/// Always allows for any permission.
///
/// Use IServiceCollection.AddAlwaysAllowAuthorization() to replace
/// IPermissionChecker with this class. This is useful for tests.
/// </summary>
public class AlwaysAllowPermissionChecker : IMoPermissionChecker
{
    public Task<bool> IsGrantedAsync(string name)
    {
        return Task.FromResult(true);
    }

    public Task<bool> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string name)
    {
        return Task.FromResult(true);
    }

    public Task<MultiplePermissionGrantResult> IsGrantedAsync(string[] names)
    {
        return IsGrantedAsync(null, names);
    }

    public Task<MultiplePermissionGrantResult> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string[] names)
    {
        return Task.FromResult(new MultiplePermissionGrantResult(names, EPermissionGrantResult.Granted));
    }
}
