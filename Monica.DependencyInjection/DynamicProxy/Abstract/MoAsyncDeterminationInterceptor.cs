using Castle.DynamicProxy;

namespace Monica.DependencyInjection.DynamicProxy.Abstract;

public class MoAsyncDeterminationInterceptor<TInterceptor>(TInterceptor MoInterceptor)
    : AsyncDeterminationInterceptor(new CastleAsyncMoInterceptorAdapter<TInterceptor>(MoInterceptor))
    where TInterceptor : IMoInterceptor;
