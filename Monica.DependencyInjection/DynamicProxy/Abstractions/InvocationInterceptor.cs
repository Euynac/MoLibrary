namespace Monica.DependencyInjection.DynamicProxy.Abstractions;

/// <summary>
/// Base class for Monica dynamic-proxy interceptors.
/// </summary>
public abstract class InvocationInterceptor : IInvocationInterceptor
{
    /// <summary>
    /// Intercepts an invocation flowing through a Monica proxy.
    /// </summary>
    /// <param name="invocation">The invocation wrapper that exposes arguments, metadata, and continuation.</param>
    public abstract Task InterceptAsync(IMethodInvocation invocation);
}
