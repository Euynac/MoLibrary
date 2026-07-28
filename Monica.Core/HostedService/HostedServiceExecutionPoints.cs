using Monica.Core.Execution;

namespace Monica.Core.HostedService;

/// <summary>
/// Stable execution points emitted by Monica hosted-service base classes.
/// </summary>
public static class HostedServiceExecutionPoints
{
    /// <summary>
    /// Represents finite hosted-service startup work.
    /// </summary>
    public static readonly ExecutionPoint Start = new("hosted-service.start");

    /// <summary>
    /// Represents finite hosted-service shutdown work.
    /// </summary>
    public static readonly ExecutionPoint Stop = new("hosted-service.stop");

    /// <summary>
    /// Represents one explicitly bounded background work item.
    /// </summary>
    public static readonly ExecutionPoint WorkItem = new("hosted-service.work-item");
}
