namespace BuildingBlocksPlatform.Core.Model;

public enum EProjectUnitType
{
    None,
    /// <summary>
    /// 应用服务
    /// </summary>
    ApplicationService,
    /// <summary>
    /// 领域服务
    /// </summary>
    DomainService,
    /// <summary>
    /// 仓储
    /// </summary>
    Repository,
    /// <summary>
    /// 领域事件
    /// </summary>
    DomainEvent,
    /// <summary>
    /// 领域事件处理程序
    /// </summary>
    DomainEventHandler,
    /// <summary>
    /// 本地事件处理程序
    /// </summary>
    LocalEventHandler,
    /// <summary>
    /// 种子数据
    /// </summary>
    Seeder,
    /// <summary>
    /// 后台工作者
    /// </summary>
    BackgroundWorker,
    /// <summary>
    /// 后台作业
    /// </summary>
    BackgroundJob,
    /// <summary>
    /// HTTP API
    /// </summary>
    HttpApi,
    /// <summary>
    /// gRPC API
    /// </summary>
    GrpcApi,
    /// <summary>
    /// 状态存储
    /// </summary>
    StateStore,
    /// <summary>
    /// 事件总线
    /// </summary>
    EventBus,
    /// <summary>
    /// Actor 模型
    /// </summary>
    Actor,
    /// <summary>
    /// 实体、聚合
    /// </summary>
    Entity,
}
