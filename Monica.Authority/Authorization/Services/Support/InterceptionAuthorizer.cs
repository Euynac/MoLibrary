using Monica.Authority.Authorization.Abstractions;
using Monica.Authority.Authorization.Models;
using Monica.Authority.Identity.Abstractions;
using Monica.DependencyInjection.DynamicProxy;
using Monica.DependencyInjection.DynamicProxy.Abstractions;

namespace Monica.Authority.Authorization.Services.Support;

public class InterceptionAuthorizer(IMethodInvocationAuthorizationService methodInvocationAuthorizationService, ICurrentPrincipalAccessor accessor)
    : InvocationInterceptor
{
    public override async Task InterceptAsync(IMethodInvocation invocation)
    {
        await AuthorizeAsync(invocation);
        await invocation.ProceedAsync();
    }

    protected virtual async Task AuthorizeAsync(IMethodInvocation invocation)
    {
        await methodInvocationAuthorizationService.CheckAsync(
            new MethodInvocationAuthorizationContext(
                invocation.Method, accessor.Principal
            )
        );
    }
}
