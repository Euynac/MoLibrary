using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Monica.Authority.Authorization.Abstractions;
using Monica.Authority.Authorization.Exceptions;
using Monica.Authority.Authorization.Models;

namespace Monica.Authority.Authorization.Services;

/// <summary>
/// Applies ASP.NET Core authorization metadata to Monica execution descriptors.
/// </summary>
public sealed class ExecutionAuthorizationService(
    IAuthorityAuthorizationPolicyProvider authorizationPolicyProvider,
    IAuthorityAuthorizationService authorizationService,
    IHttpContextAccessor httpContextAccessor)
    : IExecutionAuthorizationService
{
    /// <inheritdoc />
    public async Task CheckAsync(
        ExecutionAuthorizationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var descriptor = context.Descriptor;
        var entryMethod = descriptor.EntryMethod;
        if (AllowsAnonymous(entryMethod, descriptor.ComponentType))
        {
            return;
        }

        var authorizationData = GetAuthorizationData(entryMethod, descriptor.ComponentType).ToArray();
        if (authorizationData.Length == 0)
        {
            return;
        }

        if (context.Principal.Identity?.IsAuthenticated is not true)
        {
            if (httpContextAccessor.HttpContext is { } httpContext)
            {
                var authenticationResult = await httpContext.AuthenticateAsync();
                cancellationToken.ThrowIfCancellationRequested();
                if (authenticationResult is { Succeeded: false, Failure: not null })
                {
                    throw new AuthorizationException(authenticationResult.Failure);
                }
            }

            throw new AuthorizationException(AuthorizationException.ExceptionType.NotLogin);
        }

        var policy = await AuthorizationPolicy.CombineAsync(authorizationPolicyProvider, authorizationData);
        cancellationToken.ThrowIfCancellationRequested();
        if (policy is null)
        {
            return;
        }

        var result = await authorizationService.AuthorizeAsync(context.Principal, null, policy);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Succeeded)
        {
            throw new AuthorizationException(result.Failure);
        }
    }

    private static bool AllowsAnonymous(MethodInfo? entryMethod, Type componentType)
    {
        return entryMethod?.GetCustomAttributes(true).OfType<IAllowAnonymous>().Any() is true
               || componentType.GetCustomAttributes(true).OfType<IAllowAnonymous>().Any();
    }

    private static IEnumerable<IAuthorizeData> GetAuthorizationData(
        MethodInfo? entryMethod,
        Type componentType)
    {
        var componentData = componentType.GetCustomAttributes(true).OfType<IAuthorizeData>();
        return entryMethod is null
            ? componentData
            : entryMethod.GetCustomAttributes(true).OfType<IAuthorizeData>().Concat(componentData);
    }
}
