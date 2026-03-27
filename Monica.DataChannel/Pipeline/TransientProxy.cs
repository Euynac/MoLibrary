using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.DataChannel.CoreCommunication;
using Monica.DataChannel.Interfaces;

namespace Monica.DataChannel.Pipeline;

/// <summary>
/// Factory for transient component proxies.
/// Creates proxy wrappers for pipeline components that use a transient lifetime.
/// </summary>
public static class TransientProxy
{
    /// <summary>
    /// Creates an endpoint proxy for components that support a transient lifetime.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="componentType">The component type.</param>
    /// <param name="entranceType">The endpoint direction.</param>
    /// <param name="metadata">Optional component metadata.</param>
    /// <returns>The endpoint proxy instance.</returns>
    public static IPipeEndpoint CreateEndpointProxy(IServiceProvider serviceProvider, Type componentType, EDataSource entranceType, object? metadata = null)
    {
        // Check whether the component requires a transient lifetime.
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
    /// Creates a transform middleware proxy for components that support a transient lifetime.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="componentType">The component type.</param>
    /// <returns>The transform middleware proxy instance.</returns>
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
    /// Creates an endpoint middleware proxy for components that support a transient lifetime.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="componentType">The component type.</param>
    /// <returns>The endpoint middleware proxy instance.</returns>
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
    /// Creates a middleware instance or returns its proxy.
    /// Middleware that implements <see cref="IComponentTransient"/> is wrapped in a proxy.
    /// All other middleware types are created directly.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="componentType">The component type.</param>
    /// <returns>The middleware instance or its proxy.</returns>
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
    /// Determines whether the component type requires a transient lifetime.
    /// </summary>
    /// <param name="componentType">The component type.</param>
    /// <returns><see langword="true"/> if the component is transient; otherwise, <see langword="false"/>.</returns>
    public static bool IsTransientComponent(Type componentType)
    {
        return typeof(IComponentTransient).IsAssignableFrom(componentType);
    }
}

/// <summary>
/// Base class for transient component proxies.
/// Provides the shared logic required to create transient component instances.
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
    /// Creates a component instance of the requested type.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="scopedServiceProvider">The scoped service provider.</param>
    /// <returns>The created instance.</returns>
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
/// Proxy for transient endpoints.
/// Delegates endpoint calls to transient endpoint instances resolved per operation.
/// </summary>
internal class TransientPipeEndpointProxy(IServiceScopeFactory serviceScopeFactory, Type componentType, EDataSource entranceType, object? metadata)
    : TransientComponentProxyBase(serviceScopeFactory, componentType, metadata), ICommunicationCore
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _disposeLock = new(1, 1);
    private bool _isInit;
    private bool _isDisposed;

    public DataPipeline Pipe { get; set; } = null!;
    public void CollectException(Exception exception, object? source = null, string? description = null, ILogger? logger = null)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var instance = CreateInstance<IPipeEndpoint>(scope.ServiceProvider);
        instance.Pipe = Pipe;
        instance.EntranceType = EntranceType;
        instance.CollectException(exception, source, description, logger);
    }

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

        await _initLock.WaitAsync(cancellationToken);
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
                await communicationCore.InitAsync(cancellationToken);
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

        await _disposeLock.WaitAsync(cancellationToken);
        try
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _isInit = false;

            using var scope = ServiceScopeFactory.CreateScope();
            var instance = CreateInstance<ICommunicationCore>(scope.ServiceProvider);
            await instance.DisposeAsync(cancellationToken);
        }
        finally
        {
            _disposeLock.Release();
        }
    }

    /// <summary>
    /// Gets the connection direction supported by the communication core.
    /// </summary>
    /// <returns>The supported connection direction.</returns>
    public EConnectionDirection SupportedConnectionDirection()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var instance = CreateInstance<ICommunicationCore>(scope.ServiceProvider);
        return instance.SupportedConnectionDirection();
    }

    /// <summary>
    /// Sends data through the proxied communication core.
    /// </summary>
    /// <param name="data">The data context.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
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
/// Proxy for transient transform middleware.
/// Resolves a fresh middleware instance for each transform operation.
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
        // Assign the pipeline reference when the middleware requires pipeline access.
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
/// Proxy for transient endpoint middleware.
/// Resolves a fresh middleware instance for each operation.
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
    public void CollectException(Exception exception, object? source = null, string? description = null, ILogger? logger = null)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var instance = CreateInstance<IPipeEndpointMiddleware>(scope.ServiceProvider);
        instance.CollectException(exception, source, description, logger);
    }
}
