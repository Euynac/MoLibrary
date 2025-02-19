using System.Reflection;
using BuildingBlocksPlatform.Authority.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using IMethodInvocationAuthorizationService = BuildingBlocksPlatform.Authority.Authorization.IMethodInvocationAuthorizationService;
using MethodInvocationAuthorizationContext = BuildingBlocksPlatform.Authority.Authorization.MethodInvocationAuthorizationContext;


namespace BuildingBlocksPlatform.Authority.Implements.Authorization;

public class MoMethodInvocationAuthorizationService(
    IMoAuthorizationPolicyProvider moAuthorizationPolicyProvider,
    IMoAuthorizationService moAuthorizationService, IHttpContextAccessor accessor)
    : IMethodInvocationAuthorizationService
{
    public async Task CheckAsync(MethodInvocationAuthorizationContext context)
    {
        if (AllowAnonymous(context))
        {
            return;
        }

        if (context.User.Identity?.IsAuthenticated is not true)
        {
            if (accessor.HttpContext is { } httpContext)
            {
                var authResult = await httpContext.AuthenticateAsync();
                if (authResult is { Succeeded: false, Failure: not null})
                {
                    throw new MoAuthorizationException(authResult.Failure);
                }

            }

            throw new MoAuthorizationException(MoAuthorizationException.ExceptionType.NotLogin);
        }


        var authorizationPolicy = await AuthorizationPolicy.CombineAsync(
            moAuthorizationPolicyProvider,
            GetAuthorizationDataAttributes(context.Method)
        );

        if (authorizationPolicy == null)
        {
            return;
        }

        var result = await moAuthorizationService.AuthorizeAsync(context.User, null, authorizationPolicy);
        if (!result.Succeeded)
        {
            throw new MoAuthorizationException(result.Failure);
        }
    }

    protected virtual bool AllowAnonymous(MethodInvocationAuthorizationContext context)
    {
        return context.Method.GetCustomAttributes(true).OfType<IAllowAnonymous>().Any();
    }

    protected virtual IEnumerable<IAuthorizeData> GetAuthorizationDataAttributes(MethodInfo methodInfo)
    {
        var attributes = methodInfo
            .GetCustomAttributes(true)
            .OfType<IAuthorizeData>();

        if (methodInfo.IsPublic && methodInfo.DeclaringType != null)
        {
            attributes = attributes
                .Union(
                    methodInfo.DeclaringType
                        .GetCustomAttributes(true)
                        .OfType<IAuthorizeData>()
                );
        }

        return attributes;
    }
}
