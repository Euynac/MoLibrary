namespace Monica.Framework.ChainTracing.Models;
/// <summary>
/// Identifies the kind of component represented by a trace node.
/// </summary>
public enum EChainTracingType
{
    
    /// <summary>
    /// Unknown trace type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Controller layer that handles HTTP requests and responses.
    /// </summary>
    Controller,
    
    /// <summary>
    /// External API call, such as a third-party service request.
    /// </summary>
    ExternalApi,
    
    /// <summary>
    /// Remote service call, such as RPC or message-based integration.
    /// </summary>
    RemoteService,
    
    /// <summary>
    /// Message queue interaction, such as RabbitMQ or Kafka.
    /// </summary>
    MessageQueue,
    
    /// <summary>
    /// Domain service layer that contains business logic.
    /// </summary>
    DomainService,
    
    /// <summary>
    /// Application service layer that orchestrates workflows.
    /// </summary>
    ApplicationService,
    
    /// <summary>
    /// State storage, such as cache or session access.
    /// </summary>
    StateStore,
    
    /// <summary>
    /// Database activity, including queries and transactions.
    /// </summary>
    Database,

    /// <summary>
    /// Repository operation.
    /// </summary>
    Repository,
    
    /// <summary>
    /// File operation, including read and write work.
    /// </summary>
    File,

    /// <summary>
    /// Other uncategorized call.
    /// </summary>      
    Other,
}
