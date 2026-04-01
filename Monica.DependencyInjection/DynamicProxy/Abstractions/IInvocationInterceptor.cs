namespace Monica.DependencyInjection.DynamicProxy.Abstractions;

/// <summary>
/// Intercepts a method invocation that flows through Monica dynamic proxies.
/// </summary>
public interface IInvocationInterceptor
{
    /// <summary>
    /// Handles an invocation and decides whether or when to continue it.
    /// </summary>
    /// <param name="invocation">The invocation wrapper that exposes arguments, target metadata, and continuation.</param>
    Task InterceptAsync(IMethodInvocation invocation);
}
