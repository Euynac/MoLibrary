using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace MoLibrary.Authority.Authorization;

public class AlwaysAllowAuthorizationService
    : IMoAuthorizationService
{
    
    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
    {
        return Task.FromResult(AuthorizationResult.Success());
    }

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
    {
        return Task.FromResult(AuthorizationResult.Success());
    }
}
