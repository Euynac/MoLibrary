namespace BuildingBlocksPlatform.DependencyInjection.DynamicProxy.Abstract;

public interface IMoInterceptor
{
    Task InterceptAsync(IMoMethodInvocation invocation);
}
