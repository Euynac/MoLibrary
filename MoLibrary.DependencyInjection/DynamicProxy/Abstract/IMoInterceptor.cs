namespace MoLibrary.DependencyInjection.DynamicProxy.Abstract;

public interface IMoInterceptor
{
    Task InterceptAsync(IMoMethodInvocation invocation);
}
