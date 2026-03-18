using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Authority.Authorization.Abstractions;
using Monica.Authority.Authorization.Exceptions;

namespace Monica.Authority.Authorization.Services;

public class MoAuthorizationService(
    IAuthorizationPolicyProvider policyProvider,
    IAuthorizationHandlerProvider handlers,
    ILogger<DefaultAuthorizationService> logger,
    IAuthorizationHandlerContextFactory contextFactory,
    IAuthorizationEvaluator evaluator,
    IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationService(policyProvider,
        handlers,
        logger,
        contextFactory,
        evaluator,
        options), IMoAuthorizationService
{
    public override async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
    {
        var result = await base.AuthorizeAsync(user, resource, requirements);
        if (!result.Succeeded)
        {
            throw new MoAuthorizationException(result.Failure);
        }
        return result;
    }

    public override async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
    {
        var result = await base.AuthorizeAsync(user, resource, policyName);
        if (!result.Succeeded)
        {
            throw new MoAuthorizationException(result.Failure);
        }
        return result;
    }
}