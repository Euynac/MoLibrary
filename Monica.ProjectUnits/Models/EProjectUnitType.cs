namespace Monica.ProjectUnits.Models;

/// <summary>
/// Identifies the architectural role represented by a discovered project unit.
/// </summary>
public enum EProjectUnitType
{
    /// <summary>
    /// No architectural role has been assigned.
    /// </summary>
    None,
    /// <summary>
    /// application services
    /// </summary>
    ApplicationService,
    /// <summary>
    /// CRUD application services that participate in automatic controller generation
    /// </summary>
    CrudApplicationService,
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
    /// Host-managed long-running or lifecycle service
    /// </summary>
    HostedService,
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
