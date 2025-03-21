using BuildingBlocksPlatform.DataChannel.CoreCommunication;
using BuildingBlocksPlatform.DataChannel.CoreCommunicationProvider.Default;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.DataChannel.Pipeline;

/// <summary>
/// 管道构建器
/// </summary>
public class DataPipelineBuilder
{
    private CommunicationMetadata? _innerEndpointMetadata;
    private CommunicationMetadata? _outerEndpointMetadata;

    public Type? InnerCoreType { get; private set; }
    public Type? OuterCoreType { get; private set; }

    private readonly List<IPipeMiddleware> _middlewares = [];
    public IReadOnlyList<IPipeMiddleware> Middlewares => _middlewares;

    /// <summary>
    /// 依赖注入的中间件类型
    /// </summary>
    private readonly List<Type> _diMiddlewares = [];
    /// <summary>
    /// 管道注册ID
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// 管道组ID
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// 设置外部通信端点为默认端点
    /// </summary>
    /// <typeparam name="TCore"></typeparam>
    /// <returns></returns>
    public DataPipelineBuilder SetOuterEndpoint<TCore>() where TCore : DefaultCore
    {
        OuterCoreType = typeof(TCore);
        return this;
    }

    /// <summary>
    /// 设置内部通信端点为默认端点
    /// </summary>
    /// <typeparam name="TCore"></typeparam>
    /// <returns></returns>
    public DataPipelineBuilder SetInnerEndpoint<TCore>() where TCore : DefaultCore
    {
        InnerCoreType = typeof(TCore);
        return this;
    }

    /// <summary>
    /// 设置内部通信端点。不设置使用默认内部端点实现，忽略处理外部来的消息。
    /// </summary>
    /// <param name="metadata"></param>
    /// <returns></returns>
    public DataPipelineBuilder SetInnerEndpoint(CommunicationMetadata metadata)
    {
        metadata.EnrichOrValidate();
        _innerEndpointMetadata = metadata;
        InnerCoreType = _innerEndpointMetadata.GetCommunicationCoreType();
        return this;
    }

    /// <summary>
    /// 设置外部通信端点。必须设置。
    /// </summary>
    /// <param name="metadata"></param>
    /// <returns></returns>
    public DataPipelineBuilder SetOuterEndpoint(CommunicationMetadata metadata)
    {
        metadata.EnrichOrValidate();
        _outerEndpointMetadata = metadata;
        OuterCoreType = _outerEndpointMetadata.GetCommunicationCoreType();
        return this;
    }

    /// <summary>
    /// 增加支持依赖注入的中间件
    /// </summary>
    /// <typeparam name="TMiddleware"></typeparam>
    /// <returns></returns>
    public DataPipelineBuilder AddPipeMiddleware<TMiddleware>() where TMiddleware : class, IPipeMiddleware
    {
        _diMiddlewares.Add(typeof(TMiddleware));
        return this;
    }

    /// <summary>
    /// 增加中间件实例
    /// </summary>
    /// <param name="middlewares"></param>
    /// <returns></returns>
    public DataPipelineBuilder AddPipeMiddleware(params IPipeMiddleware[] middlewares)
    {
        _middlewares.AddRange(middlewares);
        return this;
    }

    /// <summary>
    /// 注册管道
    /// </summary>
    /// <param name="id"></param>
    /// <param name="groupId"></param>
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
    /// 构建双向通信管道
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    internal DataPipeline Build(IServiceProvider provider)
    {
        //出入Endpoint是单例注册。

        var innerEndpoint = _innerEndpointMetadata != null ?
            (IPipeEndpoint) ActivatorUtilities.CreateInstance(provider, InnerCoreType!, _innerEndpointMetadata)
            : (IPipeEndpoint) ActivatorUtilities.CreateInstance(provider, InnerCoreType!);
        innerEndpoint.EntranceType = EDataSource.Inner;

        var outerEndpoint = _outerEndpointMetadata != null ?
            (IPipeEndpoint) ActivatorUtilities.CreateInstance(provider, OuterCoreType!, _outerEndpointMetadata)
            : (IPipeEndpoint) ActivatorUtilities.CreateInstance(provider, OuterCoreType!);

        outerEndpoint.EntranceType = EDataSource.Outer;

        var pipe = new DataPipeline(innerEndpoint, outerEndpoint, Id, GroupId);

        foreach (var type in _diMiddlewares)
        {
            _middlewares.Add((IPipeMiddleware) ActivatorUtilities.CreateInstance(provider, type));
        }

        pipe.SetMiddlewares(_middlewares);
        DataChannelCentral.RegisterPipeline(pipe);
        return pipe;
    }
}