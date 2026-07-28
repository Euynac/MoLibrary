namespace Monica.ProjectUnits.Models;

/// <summary>
/// Identifies the architectural role represented by a discovered project unit.
/// </summary>
/// <remarks>
/// Numeric values are part of the serialized compatibility contract. Assign new roles only by appending a new value.
/// </remarks>
public enum EProjectUnitType
{
    /// <summary>
    /// No architectural role has been assigned.
    /// </summary>
    None = 0,
    /// <summary>
    /// Application service.
    /// </summary>
    ApplicationService = 1,
    /// <summary>
    /// CRUD application service that participates in automatic controller generation.
    /// </summary>
    CrudApplicationService = 2,
    /// <summary>
    /// Domain service.
    /// </summary>
    DomainService = 3,
    /// <summary>
    /// Repository.
    /// </summary>
    Repository = 4,
    /// <summary>
    /// Domain event.
    /// </summary>
    DomainEvent = 5,
    /// <summary>
    /// Distributed domain-event handler.
    /// </summary>
    DomainEventHandler = 6,
    /// <summary>
    /// Local event handler.
    /// </summary>
    LocalEventHandler = 7,
    /// <summary>
    /// Startup data seeder.
    /// </summary>
    Seeder = 8,
    /// <summary>
    /// Recurring scheduled job.
    /// </summary>
    RecurringJob = 9,
    /// <summary>
    /// Triggered job.
    /// </summary>
    TriggeredJob = 10,
    /// <summary>
    /// HTTP API.
    /// </summary>
    HttpApi = 11,
    /// <summary>
    /// gRPC API.
    /// </summary>
    GrpcApi = 12,
    /// <summary>
    /// State store.
    /// </summary>
    StateStore = 13,
    /// <summary>
    /// Event bus.
    /// </summary>
    EventBus = 14,
    /// <summary>
    /// Actor.
    /// </summary>
    Actor = 15,
    /// <summary>
    /// Entity or aggregate.
    /// </summary>
    Entity = 16,
    /// <summary>
    /// Request DTO.
    /// </summary>
    RequestDto = 17,
    /// <summary>
    /// Configuration model.
    /// </summary>
    Configuration = 18,
    /// <summary>
    /// Host-managed long-running or lifecycle service.
    /// </summary>
    HostedService = 19
}
