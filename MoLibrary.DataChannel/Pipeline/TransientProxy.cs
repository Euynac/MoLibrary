using Microsoft.Extensions.DependencyInjection;
using MoLibrary.DataChannel.CoreCommunication;
using MoLibrary.DataChannel.Interfaces;

namespace MoLibrary.DataChannel.Pipeline;

/// <summary>
/// Transient组件代理工厂
/// 用于创建各种组件的Transient代理
/// </summary>
public static class TransientProxy
{
    /// <summary>
    /// 创建支持Transient生命周期的端点代理
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="componentType">组件类型</param>
    /// <param name="entranceType">端点方向</param>
    /// <param name="metadata">组件元数据</param>
    /// <returns>端点代理实例</returns>
    public static IPipeEndpoint CreateEndpointProxy(IServiceProvider serviceProvider, Type componentType, EDataSource entranceType, object? metadata = null)
    {
        // 检查组件是否需要Transient生命周期
        if (IsTransientComponent(componentType))
        {
            var serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            return new TransientPipeEndpointProxy(serviceScopeFactory, componentType, entranceType, metadata);
        }

        if (metadata != null)
        {
            return (IPipeEndpoint) ActivatorUtilities.CreateInstance(serviceProvider, componentType, metadata);
        }

        return (IPipeEndpoint) ActivatorUtilities.CreateInstance(serviceProvider, componentType);

    }

    /// <summary>
    /// 创建支持Transient生命周期的转换中间件代理
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="componentType">组件类型</param>
    /// <returns>转换中间件代理实例</returns>
    private static IPipeTransformMiddleware CreateTransformMiddlewareProxy(IServiceProvider serviceProvider, Type componentType)
    {
        if (!IsTransientComponent(componentType))
        {
            return (IPipeTransformMiddleware) ActivatorUtilities.CreateInstance(serviceProvider, componentType);
        }

        var serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        return new TransientPipeTransformMiddlewareProxy(serviceScopeFactory, componentType);
    }

    /// <summary>
    /// 创建支持Transient生命周期的端点中间件代理
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="componentType">组件类型</param>
    /// <returns>端点中间件代理实例</returns>
    private static IPipeEndpointMiddleware CreateEndpointMiddlewareProxy(IServiceProvider serviceProvider, Type componentType)
    {
        if (!IsTransientComponent(componentType))
        {
            return (IPipeEndpointMiddleware) ActivatorUtilities.CreateInstance(serviceProvider, componentType);
        }

        var serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        return new TransientPipeEndpointMiddlewareProxy(serviceScopeFactory, componentType);
    }

    /// <summary>
    /// 创建或返回中间件实例
    /// 如果中间件实现了IComponentTransient接口，则创建代理
    /// 否则直接返回已创建的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="componentType">组件类型</param>
    /// <returns>中间件实例或其代理</returns>
    public static IPipeMiddleware CreateMiddlewareProxy(
        IServiceProvider serviceProvider,
        Type componentType)
    {
        if (componentType.IsAssignableTo(typeof(IPipeTransformMiddleware)))
        {
            return CreateTransformMiddlewareProxy(serviceProvider, componentType);
        }

        if (componentType.IsAssignableTo(typeof(IPipeEndpointMiddleware)))
        {
            return CreateEndpointMiddlewareProxy(serviceProvider, componentType);
        }

        throw new InvalidOperationException($"中间件类型 {componentType.FullName} 无法创建实例");
    }

    /// <summary>
    /// 检查组件类型是否需要Transient生命周期
    /// </summary>
    /// <param name="componentType">组件类型</param>
    /// <returns>是否为Transient组件</returns>
    public static bool IsTransientComponent(Type componentType)
    {
        return typeof(IComponentTransient).IsAssignableFrom(componentType);
    }
}

/// <summary>
/// 组件代理基类
/// 提供创建Transient实例的基本功能
/// </summary>
internal abstract class TransientComponentProxyBase(
    IServiceScopeFactory serviceScopeFactory,
    Type componentType,
    object? metadata)
{
    protected readonly IServiceScopeFactory ServiceScopeFactory = serviceScopeFactory;
    protected readonly Type ComponentType = componentType;
    protected readonly object? Metadata = metadata;

    /// <summary>
    /// 创建特定类型的组件实例
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="scopedServiceProvider">从外部scope提供的ServiceProvider</param>
    /// <returns>创建的实例</returns>
    protected T CreateInstance<T>(IServiceProvider scopedServiceProvider) where T : class
    {
        var obj = Metadata != null
            ? ActivatorUtilities.CreateInstance(scopedServiceProvider, ComponentType, Metadata)
            : ActivatorUtilities.CreateInstance(scopedServiceProvider, ComponentType);

        if (obj is T typedInstance)
        {
            return typedInstance;
        }

        throw new Exception($"无法创建类型为 {ComponentType.FullName} 的实例");
    }
}

/// <summary>
/// 端点代理类
/// 实现对Transient端点的代理
/// </summary>
internal class TransientPipeEndpointProxy(IServiceScopeFactory serviceScopeFactory, Type componentType, EDataSource entranceType, object? metadata)
    : TransientComponentProxyBase(serviceScopeFactory, componentType, metadata), ICommunicationCore
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _disposeLock = new(1, 1);
    private bool _isInit;
    private bool _isDisposed;

    public DataPipeline Pipe { get; set; } = null!;

    public EDataSource EntranceType { get; set; } = entranceType;

    public async Task ReceiveDataAsync(DataContext data)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var instance = CreateInstance<IPipeEndpoint>(scope.ServiceProvider);
        instance.Pipe = Pipe;
        instance.EntranceType = EntranceType;
        await instance.ReceiveDataAsync(data);
    }

    public dynamic GetMetadata()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var instance = CreateInstance<IPipeEndpoint>(scope.ServiceProvider);
        return instance.GetMetadata();
    }

    public async Task InitAsync(CancellationToken cancellationToken = default)
    {
        if (_isInit)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInit)
                return;

            _isInit = true;

            using var scope = ServiceScopeFactory.CreateScope();
            var instance = CreateInstance<IPipeEndpoint>(scope.ServiceProvider);
            instance.Pipe = Pipe;
            instance.EntranceType = EntranceType;

            if (instance is ICommunicationCore communicationCore)
            {
                await communicationCore.InitAsync();
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task DisposeAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            return;

        await _disposeLock.WaitAsync();
        try
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _isInit = false;

            using var scope = ServiceScopeFactory.CreateScope();
            var instance = CreateInstance<ICommunicationCore>(scope.ServiceProvider);
            await instance.DisposeAsync();
        }
        finally
        {
            _disposeLock.Release();
        }
    }

    /// <summary>
    /// 获取通信核心支持的连接方向
    /// </summary>
    /// <returns>连接方向</returns>
    public EConnectionDirection SupportedConnectionDirection()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var instance = CreateInstance<ICommunicationCore>(scope.ServiceProvider);
        return instance.SupportedConnectionDirection();
    }

    /// <summary>
    /// 发送数据
    /// </summary>
    /// <param name="data">数据上下文</param>
    /// <returns>异步任务</returns>
    public async Task SendDataAsync(DataContext data)
    {
        if (!_isInit)
        {
            await InitAsync();
        }

        using var scope = ServiceScopeFactory.CreateScope();
        var instance = CreateInstance<ICommunicationCore>(scope.ServiceProvider);
        instance.Pipe = Pipe;
        instance.EntranceType = EntranceType;
        await instance.SendDataAsync(data);
    }
}

/// <summary>
/// 转换中间件代理类
/// 实现对Transient转换中间件的代理
/// </summary>
internal class TransientPipeTransformMiddlewareProxy(
    IServiceScopeFactory serviceScopeFactory,
    Type componentType)
    : TransientComponentProxyBase(serviceScopeFactory, componentType, null), IPipeTransformMiddleware
{
    public async Task<DataContext> PassAsync(DataContext context)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var instance = CreateInstance<IPipeTransformMiddleware>(scope.ServiceProvider);
        // 如果中间件需要访问管道，则设置管道引用
        if (instance is IWantAccessPipeline wantAccess && Pipeline != null)
        {
            wantAccess.Pipe = Pipeline;
        }

        return await instance.PassAsync(context);
    }

    public dynamic GetMetadata()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var instance = CreateInstance<IPipeTransformMiddleware>(scope.ServiceProvider);
        return instance.GetMetadata();
    }

    public DataPipeline? Pipeline { get; set; }
}

/// <summary>
/// 端点中间件代理类
/// 实现对Transient端点中间件的代理
/// </summary>
internal class TransientPipeEndpointMiddlewareProxy(
    IServiceScopeFactory serviceScopeFactory,
    Type componentType)
    : TransientComponentProxyBase(serviceScopeFactory, componentType, null), IPipeEndpointMiddleware
{
    public dynamic GetMetadata()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var instance = CreateInstance<IPipeEndpointMiddleware>(scope.ServiceProvider);
        return instance.GetMetadata();
    }

    public DataPipeline Pipe { get; set; } = null!;
}