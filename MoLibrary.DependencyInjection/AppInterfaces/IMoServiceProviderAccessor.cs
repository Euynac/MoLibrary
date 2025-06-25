namespace MoLibrary.DependencyInjection.AppInterfaces;

/// <summary>
/// Provides access to an <see cref="IServiceProvider"/> instance.
/// </summary>
public interface IMoServiceProviderAccessor
{
    /// <summary>
    /// Gets the service provider associated with the current instance.
    /// </summary>
    IServiceProvider ServiceProvider { get; }
}
/// <summary>
/// Provides access to an <see cref="IServiceProvider"/> instance.
/// </summary>
public interface IMoServiceProviderInjector
{
    /// <summary>
    /// Gets the service provider associated with the current instance.
    /// </summary>
    IMoServiceProvider MoProvider { get; set; }
}
/// <summary>
/// Use to inject service provider to substitute property injection.
/// </summary>
public interface IMoServiceProvider
{
    /// <summary>
    /// Use default service provider.
    /// </summary>
    IServiceProvider ServiceProvider { get; }
}

//TODO 优化为LazyServiceProvider以及多次获取无需重新创建？
//TODO 需要测试当前实现在Scoped、Transient、Singleton下的行为异同
//巨坑：IServiceProvider的注入和实现要求有关，比如当前是ISingleton模式注册，那么获取到的IServiceProvider就是以Singleton模式去获取其他实例
public class DefaultMoServiceProvider(IServiceProvider serviceProvider) : IMoServiceProvider
{
    public IServiceProvider ServiceProvider => serviceProvider;
}