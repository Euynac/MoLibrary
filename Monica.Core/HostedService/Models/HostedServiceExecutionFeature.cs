namespace Monica.Core.HostedService.Models;

/// <summary>
/// Identifies the hosted service that initiated one lifecycle or finite-work-item execution.
/// </summary>
/// <param name="ServiceName">The observable hosted-service name.</param>
/// <param name="Phase">The finite lifecycle phase.</param>
/// <param name="ServiceType">The concrete hosted-service type.</param>
public sealed record HostedServiceExecutionFeature(
    string ServiceName,
    HostedServiceLifecyclePhase Phase,
    Type ServiceType);

/// <summary>
/// Identifies the finite hosted-service phase represented by an execution.
/// </summary>
public enum HostedServiceLifecyclePhase
{
    /// <summary>
    /// Service startup.
    /// </summary>
    Start,

    /// <summary>
    /// Service shutdown.
    /// </summary>
    Stop,

    /// <summary>
    /// One explicitly bounded work item, never the permanent background loop.
    /// </summary>
    WorkItem
}
