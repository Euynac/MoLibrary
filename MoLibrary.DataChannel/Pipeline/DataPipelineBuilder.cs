using MoLibrary.DataChannel.CoreCommunication;
using MoLibrary.DataChannel.CoreCommunicationProvider.Default;

namespace MoLibrary.DataChannel.Pipeline;

/// <summary>
/// 数据管道构建器
/// 提供流式API用于创建和配置数据管道
/// 负责组装端点、中间件和其他组件形成完整的管道
/// </summary>
public class DataPipelineBuilder
{
    private CommunicationMetadata? _innerEndpointMetadata;
    private CommunicationMetadata? _outerEndpointMetadata;

    /// <summary>
    /// 内部通信核心类型
    /// </summary>
    public Type? InnerCoreType { get; private set; }
    
    /// <summary>
    /// 外部通信核心类型
    /// </summary>
    public Type? OuterCoreType { get; private set; }

    private readonly List<IPipeMiddleware> _middlewares = [];
    
    /// <summary>
    /// 已添加的中间件实例集合
    /// </summary>
    public IReadOnlyList<IPipeMiddleware> Middlewares => _middlewares;

    /// <summary>
    /// 依赖注入的中间件类型集合
    /// 这些中间件将在构建时从服务容器中解析
    /// </summary>
    private readonly List<Type> _diMiddlewares = [];
    
    /// <summary>
    /// 管道注册ID
    /// 用于唯一标识此管道
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// 管道组ID
    /// 用于将相关管道组织在一起
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// 设置外部通信端点为默认端点
    /// </summary>
    /// <typeparam name="TCore">外部通信端点类型，必须继承自DefaultCore</typeparam>
    /// <returns>构建器实例，用于链式调用</returns>
    public DataPipelineBuilder SetOuterEndpoint<TCore>() where TCore : DefaultCore
    {
        OuterCoreType = typeof(TCore);
        return this;
    }

    /// <summary>
    /// 设置内部通信端点为默认端点
    /// </summary>
    /// <typeparam name="TCore">内部通信端点类型，必须继承自DefaultCore</typeparam>
    /// <returns>构建器实例，用于链式调用</returns>
    public DataPipelineBuilder SetInnerEndpoint<TCore>() where TCore : DefaultCore
    {
        InnerCoreType = typeof(TCore);
        return this;
    }

    /// <summary>
    /// 设置内部通信端点
    /// 不设置时将使用默认内部端点实现，忽略处理外部来的消息
    /// </summary>
    /// <param name="metadata">通信元数据，包含端点配置信息</param>
    /// <returns>构建器实例，用于链式调用</returns>
    public DataPipelineBuilder SetInnerEndpoint(CommunicationMetadata metadata)
    {
        metadata.EnrichOrValidate();
        _innerEndpointMetadata = metadata;
        InnerCoreType = _innerEndpointMetadata.GetCommunicationCoreType();
        return this;
    }

    /// <summary>
    /// 设置外部通信端点
    /// 必须设置，否则无法构建管道
    /// </summary>
    /// <param name="metadata">通信元数据，包含端点配置信息</param>
    /// <returns>构建器实例，用于链式调用</returns>
    public DataPipelineBuilder SetOuterEndpoint(CommunicationMetadata metadata)
    {
        metadata.EnrichOrValidate();
        _outerEndpointMetadata = metadata;
        OuterCoreType = _outerEndpointMetadata.GetCommunicationCoreType();
        return this;
    }

    /// <summary>
    /// 添加支持依赖注入的中间件
    /// 中间件实例将在构建时从服务容器中解析
    /// </summary>
    /// <typeparam name="TMiddleware">中间件类型，必须实现IPipeMiddleware接口</typeparam>
    /// <returns>构建器实例，用于链式调用</returns>
    public DataPipelineBuilder AddPipeMiddleware<TMiddleware>() where TMiddleware : class, IPipeMiddleware
    {
        _diMiddlewares.Add(typeof(TMiddleware));
        return this;
    }

    /// <summary>
    /// 添加中间件实例
    /// 直接添加已创建的中间件实例到管道
    /// </summary>
    /// <param name="middlewares">要添加的中间件实例数组</param>
    /// <returns>构建器实例，用于链式调用</returns>
    public DataPipelineBuilder AddPipeMiddleware(params IPipeMiddleware[] middlewares)
    {
        _middlewares.AddRange(middlewares);
        return this;
    }

    /// <summary>
    /// 注册管道到中央管理器
    /// 完成管道配置并将其添加到DataChannelCentral
    /// </summary>
    /// <param name="id">管道唯一标识符</param>
    /// <param name="groupId">可选的管道组标识符</param>
    /// <exception cref="Exception">外部端点未设置时抛出异常</exception>
    public void Register(string id, string? groupId = null)
    {
        if (OuterCoreType == null) throw new Exception("You must set outer endpoint for data pipeline");
        if (_innerEndpointMetadata == null && InnerCoreType == null)
        {
            SetInnerEndpoint(new MetadataForDefault());
        }
        Id = id;
        GroupId = groupId;
        DataChannelCentral.RegisterBuilder(this);
    }

    /// <summary>
    /// 构建数据管道
    /// 创建并连接所有端点和中间件，形成完整的数据管道
    /// 对于标记为Transient的组件，将创建代理
    /// </summary>
    /// <param name="provider">服务提供者，用于解析依赖</param>
    /// <returns>构建完成的数据管道实例</returns>
    internal DataPipeline Build(IServiceProvider provider)
    {
        // 创建内部端点
        var innerEndpoint = TransientProxy.CreateEndpointProxy(provider, InnerCoreType!, EDataSource.Inner, _innerEndpointMetadata);

        // 创建外部端点
        var outerEndpoint = TransientProxy.CreateEndpointProxy(provider, OuterCoreType!, EDataSource.Outer, _outerEndpointMetadata);
        outerEndpoint.EntranceType = EDataSource.Outer;

        // 创建管道
        var pipe = new DataPipeline(innerEndpoint, outerEndpoint, Id, GroupId);

        // 创建中间件
        var middlewaresList = new List<IPipeMiddleware>(_middlewares);
        
        // 添加依赖注入的中间件
        foreach (var type in _diMiddlewares)
        {
            var middleware = TransientProxy.CreateMiddlewareProxy(provider, type);
            middlewaresList.Add(middleware);
        }

        pipe.SetMiddlewares(middlewaresList);
        DataChannelCentral.RegisterPipeline(pipe);
        return pipe;
    }
}