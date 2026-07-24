namespace Monica.ProjectUnits.Models;

/// <summary>
/// Describes a discovered domain event and a representative serializable payload structure.
/// </summary>
public sealed class ProjectUnitDomainEventInfo
{
    /// <summary>
    /// Gets the domain-event project-unit summary.
    /// </summary>
    public required ProjectUnitSummary Info { get; init; }

    /// <summary>
    /// Gets the representative payload structure.
    /// </summary>
    public object? Structure { get; init; }
}
