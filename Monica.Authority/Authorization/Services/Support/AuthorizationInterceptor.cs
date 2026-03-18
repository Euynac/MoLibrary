using Monica.Authority.Authorization;
using Monica.Authority.Security;
using Monica.DependencyInjection.DynamicProxy;
using Monica.DependencyInjection.DynamicProxy.Abstract;

namespace Monica.Authority.Implements.Authorization;

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
