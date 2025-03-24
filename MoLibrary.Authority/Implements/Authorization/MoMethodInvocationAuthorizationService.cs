using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using MoLibrary.Authority.Authorization;
using Authorization_IMethodInvocationAuthorizationService = MoLibrary.Authority.Authorization.IMethodInvocationAuthorizationService;
using IMethodInvocationAuthorizationService = MoLibrary.Authority.Authorization.IMethodInvocationAuthorizationService;
using MethodInvocationAuthorizationContext = MoLibrary.Authority.Authorization.MethodInvocationAuthorizationContext;


namespace MoLibrary.Authority.Implements.Authorization;

public class MoMethodInvocationAuthorizationService(
    IMoAuthorizationPolicyProvider moAuthorizationPolicyProvider,
    IMoAuthorizationService moAuthorizationService, IHttpContextAccessor accessor)
    : Authorization_IMethodInvocationAuthorizationService
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
