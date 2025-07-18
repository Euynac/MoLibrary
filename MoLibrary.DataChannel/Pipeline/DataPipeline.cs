using Microsoft.Extensions.Logging;
using MoLibrary.Core.Extensions;
using MoLibrary.DataChannel.CoreCommunication;
using MoLibrary.DataChannel.Exceptions;
using MoLibrary.DataChannel.Interfaces;
using MoLibrary.DataChannel.Modules;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DataChannel.Pipeline;

/// <summary>
/// 数据管道类
/// 作为数据通道的核心组件，负责数据的传输、转换和处理
/// 管理数据端点和中间件的连接和协作
/// </summary>
public class DataPipeline
{
    /// <summary>
    /// 内部端点
    /// 处理从外部流向内部的数据
    /// </summary>
    public IPipeEndpoint InnerEndpoint { get; set; }
    
    /// <summary>
    /// 外部端点
    /// 处理从内部流向外部的数据
    /// </summary>
    public IPipeEndpoint OuterEndpoint { get; set; }

    /// <summary>
    /// 管道唯一标识符
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// 管道组标识符
    /// 用于将多个相关管道分组管理
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// 是否已成功初始化
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 是否正在初始化
    /// </summary>
    public bool IsInitializing { get; private set; }

    /// <summary>
    /// 是否已不可用
    /// 当初始化失败或发生致命错误时设置为true
    /// </summary>
    public bool IsNotAvailable { get; set; }

    /// <summary>
    /// 异常池，用于收集和管理管道运行中的异常
    /// </summary>
    public ExceptionPool ExceptionPool { get; private set; }

    /// <summary>
    /// 是否存在异常
    /// </summary>
    public bool HasExceptions => ExceptionPool?.HasExceptions ?? false;

    /// <summary>
    /// 初始化数据管道的新实例
    /// </summary>
    /// <param name="innerEndpoint">内部端点</param>
    /// <param name="outerEndpoint">外部端点</param>
    /// <param name="id">管道标识符</param>
    /// <param name="groupId">可选的管道组标识符</param>
    internal DataPipeline(IPipeEndpoint innerEndpoint, IPipeEndpoint outerEndpoint, string id, string? groupId = null)
    {
        InnerEndpoint = innerEndpoint;
        OuterEndpoint = outerEndpoint;
        Id = id;
        GroupId = groupId;
        
        ExceptionPool = new ExceptionPool(id, DataChannelCentral.Setting.RecentExceptionToKeep);
    }

    /// <summary>
    /// 创建新的数据管道构建器
    /// </summary>
    /// <returns>数据管道构建器实例</returns>
    public static DataPipelineBuilder Create() => new();

    /// <summary>
    /// 端点中间件列表
    /// 处理端点之间的数据交互
    /// </summary>
    public List<IPipeEndpointMiddleware> EndpointMiddlewares { get; private set; } = [];
    
    /// <summary>
    /// 转换中间件列表
    /// 处理数据的转换和处理
    /// </summary>
    public List<IPipeTransformMiddleware> TransformMiddlewares { get; private set; } = [];

    /// <summary>
    /// 获取所有中间件的集合
    /// </summary>
    /// <returns>所有管道中间件的枚举</returns>
    internal IEnumerable<IPipeMiddleware> GetMiddlewares()
    {
        foreach (var endpointMiddleware in EndpointMiddlewares)
        {
            yield return endpointMiddleware;
        }

        foreach (var transformMiddleware in TransformMiddlewares)
        {
            yield return transformMiddleware;
        }
    }

    /// <summary>
    /// 获取所有端点的集合
    /// </summary>
    /// <returns>所有管道端点的枚举</returns>
    internal IEnumerable<IPipeEndpoint> GetEndpoints()
    {
        yield return InnerEndpoint;
        yield return OuterEndpoint;
    }

    /// <summary>
    /// 获取所有管道组件的集合
    /// 包括端点和中间件
    /// </summary>
    /// <returns>所有管道组件的枚举</returns>
    internal IEnumerable<IPipeComponent> GetComponents()
    {
        foreach (var endpoint in GetEndpoints())
        {
            yield return endpoint;
        }

        foreach (var middleware in GetMiddlewares())
        {
            yield return middleware;
        }
    }

    /// <summary>
    /// 设置管道中间件
    /// 将中间件按类型分配到相应的集合中
    /// </summary>
    /// <param name="middlewares">要设置的中间件列表</param>
    internal void SetMiddlewares(IReadOnlyList<IPipeMiddleware> middlewares)
    {
        EndpointMiddlewares = middlewares.OfType<IPipeEndpointMiddleware>().ToList();
        TransformMiddlewares = middlewares.OfType<IPipeTransformMiddleware>().ToList();
    }

    /// <summary>
    /// 收集异常信息到异常池
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="source">异常来源对象</param>
    public void CollectException(Exception exception, object source)
    {
        ExceptionPool.AddException(exception, source);
    }

    /// <summary>
    /// 发送数据到管道
    /// 数据将经过转换中间件处理，然后根据入口方向发送到相应的端点
    /// </summary>
    /// <param name="data">要发送的数据上下文</param>
    /// <returns>表示异步操作的任务</returns>
    public async Task SendDataAsync(DataContext data)
    {
        // 处理转换中间件
        await TransformMiddlewares.DoAsync(async p =>
        {
            try
            {
                data = await p.PassAsync(data);
            }
            catch (Exception ex)
            {
                CollectException(ex, p);
                throw; // 重新抛出异常以保持原有行为
            }
        });
      

        // 处理端点
        try
        {
            if (data.Source == EDataSource.Outer)
            {
                await InnerEndpoint.ReceiveDataAsync(data);
            }
            else if(data.Source == EDataSource.Inner)
            {
                await OuterEndpoint.ReceiveDataAsync(data);
            }
        }
        catch (Exception ex)
        {
            var targetEndpoint = data.Source == EDataSource.Outer ? InnerEndpoint : OuterEndpoint;
            CollectException(ex, targetEndpoint);
            throw; // 重新抛出异常以保持原有行为
        }
    }

    /// <summary>
    /// 初始化管道及其组件
    /// 设置管道引用并初始化所有通信核心
    /// </summary>
    /// <returns>初始化结果，包含成功状态和可能的错误信息</returns>
    internal async Task<Res> InitAsync(CancellationToken cancellationToken = default)
    {
        if(IsInitializing)
        {
            return Res.Ok("管道正在初始化中");
        }
        IsInitializing = true;
        InnerEndpoint.Pipe = this;
        OuterEndpoint.Pipe = this;
        GetMiddlewares().OfType<IWantAccessPipeline>().Do(p => p.Pipe = this);
        
        foreach (var communicationCore in GetEndpoints().OfType<ICommunicationCore>())
        {
            try
            {
                await communicationCore.InitAsync(cancellationToken);
            }
            catch (Exception e)
            {
                // 收集异常信息
                CollectException(e, communicationCore);
                
                IsNotAvailable = true;
                IsInitializing = false;
                DataChannelCentral.Logger.LogError(e, "DataPipeline:{Id}初始化失败", Id);
                return $"DataPipeline:{Id}初始化失败:{e.GetMessageRecursively()}";
            }
        }

        IsInitialized = true;
        IsInitializing = false;
        return Res.Ok("管道初始化成功");
    }

    /// <summary>
    /// 释放管道及其组件的资源
    /// </summary>
    /// <returns>表示异步操作的任务</returns>
    internal async Task DisposeAsync()
    {
        await GetEndpoints().OfType<ICommunicationCore>().DoAsync(async p => await p.DisposeAsync());
        ExceptionPool?.Dispose();
        IsInitialized = false;
    }
}