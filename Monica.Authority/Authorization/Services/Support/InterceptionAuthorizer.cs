using Monica.Authority.Authorization.Abstractions;
using Monica.Authority.Authorization.Models;
using Monica.Authority.Identity.Abstractions;
using Monica.DependencyInjection.DynamicProxy;
using Monica.DependencyInjection.DynamicProxy.Abstract;

namespace Monica.Authority.Authorization.Services.Support;

public class InterceptionAuthorizer(IMethodInvocationAuthorizationService methodInvocationAuthorizationService, ICurrentPrincipalAccessor accessor)
    : MoInterceptor
{
    public override async Task InterceptAsync(IMoMethodInvocation invocation)
    {
        await AuthorizeAsync(invocation);
        await invocation.ProceedAsync();
    }

    protected virtual async Task AuthorizeAsync(IMoMethodInvocation invocation)
    {
        await methodInvocationAuthorizationService.CheckAsync(
            new MethodInvocationAuthorizationContext(
                invocation.Method, accessor.Principal
            )
        );
    }
}
