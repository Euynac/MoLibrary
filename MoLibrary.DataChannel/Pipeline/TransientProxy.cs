using Microsoft.Extensions.DependencyInjection;
using MoLibrary.DataChannel.CoreCommunication;
using MoLibrary.DataChannel.Interfaces;
using MoLibrary.Tool.MoResponse;
using System.Diagnostics.CodeAnalysis;

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
            return new TransientPipeEndpointProxy(serviceProvider, componentType, entranceType, metadata);

        if (metadata != null)
        {
            return (IPipeEndpoint)ActivatorUtilities.CreateInstance(serviceProvider, componentType, metadata);
        }

        return (IPipeEndpoint)ActivatorUtilities.CreateInstance(serviceProvider, componentType);

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
            return (IPipeTransformMiddleware)ActivatorUtilities.CreateInstance(serviceProvider, componentType);
        }

        return new TransientPipeTransformMiddlewareProxy(serviceProvider, componentType);
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
            return (IPipeEndpointMiddleware)ActivatorUtilities.CreateInstance(serviceProvider, componentType);
        }

        return new TransientPipeEndpointMiddlewareProxy(serviceProvider, componentType);
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
    IServiceProvider serviceProvider,
    Type componentType,
    object? metadata)
{
    protected readonly IServiceProvider ServiceProvider = serviceProvider;
    protected readonly Type ComponentType = componentType;
    protected readonly object? Metadata = metadata;

    /// <summary>
    /// 创建组件的新实例
    /// </summary>
    /// <returns>组件实例</returns>
    protected object CreateInstance()
    {
        return Metadata != null
            ? ActivatorUtilities.CreateInstance(ServiceProvider, ComponentType, Metadata)
            : ActivatorUtilities.CreateInstance(ServiceProvider, ComponentType);
    }

    /// <summary>
    /// 尝试创建特定类型的组件实例
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="instance">创建的实例</param>
    /// <returns>是否成功创建</returns>
    protected bool TryCreateInstance<T>([NotNullWhen(true)] out T? instance) where T : class
    {
        var obj = CreateInstance();
        if (obj is T typedInstance)
        {
            instance = typedInstance;
            return true;
        }

        instance = null;
        return false;
    }
}

/// <summary>
/// 端点代理类
/// 实现对Transient端点的代理
/// </summary>
internal class TransientPipeEndpointProxy(IServiceProvider serviceProvider, Type componentType, EDataSource entranceType, object? metadata)
    : TransientComponentProxyBase(serviceProvider, componentType, metadata), IPipeEndpoint, ICommunicationCore
{
    private IPipeEndpoint? _cachedInstance;
    private bool _isDisposed;

    public DataPipeline Pipe { get; set; } = null!;

    public EDataSource EntranceType { get; set; } = entranceType;

    public async Task ReceiveDataAsync(DataContext data)
    {
        if (TryCreateInstance<IPipeEndpoint>(out var instance))
        {
            instance.Pipe = Pipe;
            instance.EntranceType = EntranceType;
            await instance.ReceiveDataAsync(data);
        }
    }

    public dynamic GetMetadata()
    {
        if (TryCreateInstance<IPipeEndpoint>(out var instance))
        {
            return instance.GetMetadata();
        }

        return new System.Dynamic.ExpandoObject();
    }

    public async Task InitAsync()
    {
        if (TryCreateInstance<IPipeEndpoint>(out var endpoint))
        {
            endpoint.Pipe = Pipe;
            endpoint.EntranceType = EntranceType;

            if (endpoint is ICommunicationCore communicationCore)
            {
                await communicationCore.InitAsync();
                _cachedInstance = endpoint;
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_isDisposed)
            return;

        if (_cachedInstance is ICommunicationCore communicationCore)
        {
            await communicationCore.DisposeAsync();
            _cachedInstance = null;
        }

        _isDisposed = true;
    }

    /// <summary>
    /// 获取通信核心支持的连接方向
    /// </summary>
    /// <returns>连接方向</returns>
    public EConnectionDirection SupportedConnectionDirection()
    {
        if (TryCreateInstance<ICommunicationCore>(out var communicationCore))
        {
            return communicationCore.SupportedConnectionDirection();
        }

        return EConnectionDirection.InputAndOutput;
    }

    /// <summary>
    /// 发送数据
    /// </summary>
    /// <param name="data">数据上下文</param>
    /// <returns>异步任务</returns>
    public async Task SendDataAsync(DataContext data)
    {
        if (TryCreateInstance<ICommunicationCore>(out var communicationCore))
        {
            communicationCore.Pipe = Pipe;
            communicationCore.EntranceType = EntranceType;
            await communicationCore.SendDataAsync(data);
        }
    }
}

/// <summary>
/// 转换中间件代理类
/// 实现对Transient转换中间件的代理
/// </summary>
internal class TransientPipeTransformMiddlewareProxy(
    IServiceProvider serviceProvider,
    Type componentType)
    : TransientComponentProxyBase(serviceProvider, componentType, null), IPipeTransformMiddleware
{
    public async Task<DataContext> PassAsync(DataContext context)
    {
        if (TryCreateInstance<IPipeTransformMiddleware>(out var middleware))
        {
            // 如果中间件需要访问管道，则设置管道引用
            if (middleware is IWantAccessPipeline wantAccess && Pipeline != null)
            {
                wantAccess.Pipe = Pipeline;
            }

            return await middleware.PassAsync(context);
        }

        return context;
    }

    public dynamic GetMetadata()
    {
        if (TryCreateInstance<IPipeTransformMiddleware>(out var middleware))
        {
            return middleware.GetMetadata();
        }

        return new System.Dynamic.ExpandoObject();
    }

    public DataPipeline? Pipeline { get; set; }
}

/// <summary>
/// 端点中间件代理类
/// 实现对Transient端点中间件的代理
/// </summary>
internal class TransientPipeEndpointMiddlewareProxy(
    IServiceProvider serviceProvider,
    Type componentType)
    : TransientComponentProxyBase(serviceProvider, componentType, null), IPipeEndpointMiddleware
{
    public dynamic GetMetadata()
    {
        if (TryCreateInstance<IPipeEndpointMiddleware>(out var middleware))
        {
            return middleware.GetMetadata();
        }

        return new System.Dynamic.ExpandoObject();
    }

    public DataPipeline Pipe { get; set; } = null!;
}