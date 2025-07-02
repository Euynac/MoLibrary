namespace MoLibrary.Core.Features.MoChainTracing.Models;
/// <summary>
/// 调用链追踪类型枚举，用于标识调用链中不同类型的组件
/// </summary>
public enum EChainTracingType
{
    
    /// <summary>
    /// 未知
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 控制器层，处理HTTP请求和响应
    /// </summary>
    Controller,
    
    /// <summary>
    /// 外部API调用，如第三方服务接口
    /// </summary>
    ExternalApi,
    
    /// <summary>
    /// 远程服务调用，如RPC、MQ等
    /// </summary>
    RemoteService,
    
    /// <summary>
    /// 消息队列，如RabbitMQ、Kafka等
    /// </summary>
    MessageQueue,
    
    /// <summary>
    /// 领域服务层，包含业务逻辑处理
    /// </summary>
    DomainService,
    
    /// <summary>
    /// 应用服务层，协调业务流程
    /// </summary>
    ApplicationService,
    
    /// <summary>
    /// 状态存储，如缓存、会话等
    /// </summary>
    StateStore,
    
    /// <summary>
    /// 数据库操作，包括查询和事务处理
    /// </summary>
    Database,

    /// <summary>
    /// 仓储操作
    /// </summary>
    Repository,
    
    /// <summary>
    /// 文件操作，包括读写文件
    /// </summary>
    File,

    /// <summary>
    /// 其他，用于未分类的调用
    /// </summary>      
    Other,
}