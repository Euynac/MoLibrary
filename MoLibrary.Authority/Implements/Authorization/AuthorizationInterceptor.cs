using MoLibrary.Authority.Authorization;
using MoLibrary.Authority.Security;
using MoLibrary.DependencyInjection.DynamicProxy;
using MoLibrary.DependencyInjection.DynamicProxy.Abstract;

namespace MoLibrary.Authority.Implements.Authorization;

public class AuthorizationInterceptor(IMethodInvocationAuthorizationService methodInvocationAuthorizationService, IMoCurrentPrincipalAccessor accessor)
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
