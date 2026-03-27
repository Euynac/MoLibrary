namespace Monica.Framework.Core.Model;

public enum EProjectUnitType
{
    None,
    /// <summary>
    /// application services
    /// </summary>
    ApplicationService,
    /// <summary>
    /// Domain services
    /// </summary>
    DomainService,
    /// <summary>
    /// warehousing
    /// </summary>
    Repository,
    /// <summary>
    /// domain events
    /// </summary>
    DomainEvent,
    /// <summary>
    /// Domain event handler
    /// </summary>
    DomainEventHandler,
    /// <summary>
    /// local event handler
    /// </summary>
    LocalEventHandler,
    /// <summary>
    /// Seed data
    /// </summary>
    Seeder,
    /// <summary>
    /// Background scheduled jobs
    /// </summary>
    RecurringJob,
    /// <summary>
    /// background job
    /// </summary>
    TriggeredJob,
    /// <summary>
    /// HTTP API
    /// </summary>
    HttpApi,
    /// <summary>
    /// gRPC API
    /// </summary>
    GrpcApi,
    /// <summary>
    /// state storage
    /// </summary>
    StateStore,
    /// <summary>
    /// event bus
    /// </summary>
    EventBus,
    /// <summary>
    /// Actor model
    /// </summary>
    Actor,
    /// <summary>
    /// entities, aggregates
    /// </summary>
    Entity,
    /// <summary>
    /// Request class
    /// </summary>
    RequestDto,
    /// <summary>
    /// Configuration class
    /// </summary>
    Configuration,
}
