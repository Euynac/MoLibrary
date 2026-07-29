using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Monica.Authority.Authorization.Abstractions;
using Monica.Authority.Authorization.Exceptions;
using Monica.Authority.Authorization.Models;
using Monica.Authority.Authorization.Services.Support;
using Monica.Core.Execution.Mvc;

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
        if (ExecutionAuthorizationMetadata.AllowsAnonymous(entryMethod, descriptor.ComponentType))
        {
            return;
        }

        var authorizationData = ExecutionAuthorizationMetadata
            .GetAuthorizationData(entryMethod, descriptor.ComponentType)
            .ToArray();
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

        // Direct MVC policies receive their action HttpContext. Other adapters keep their own target/input resource even
        // when they happen to execute within an ambient HTTP request.
        var resource = context.Input is MvcActionExecutionInput mvcInput
            ? mvcInput.HttpContext
            : context.DefaultResource;
        var result = await authorizationService.AuthorizeAsync(context.Principal, resource, policy);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Succeeded)
        {
            throw new AuthorizationException(result.Failure);
        }
    }
}
