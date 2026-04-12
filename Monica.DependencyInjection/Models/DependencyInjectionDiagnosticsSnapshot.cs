namespace Monica.DependencyInjection.Models;

/// <summary>
/// Represents a point-in-time snapshot of the finalized service collection.
/// </summary>
public sealed class DependencyInjectionDiagnosticsSnapshot
{
    /// <summary>
    /// Gets when the snapshot was materialized.
    /// </summary>
    public required DateTimeOffset CapturedAt { get; init; }

    /// <summary>
    /// Gets every descriptor from the finalized service collection.
    /// </summary>
    public IReadOnlyList<DependencyInjectionDescriptorInfo> Descriptors { get; init; } = [];

    /// <summary>
    /// Gets the total descriptor count.
    /// </summary>
    public int TotalDescriptorCount { get; init; }

    /// <summary>
    /// Gets warnings and errors emitted while Monica evaluated conventional registration.
    /// </summary>
    public IReadOnlyList<DependencyInjectionAutoRegistrationIssueInfo> AutoRegistrationIssues { get; init; } = [];

    /// <summary>
    /// Gets the number of descriptors created through Monica conventional registration.
    /// </summary>
    public int AutoRegisteredDescriptorCount { get; init; }

    /// <summary>
    /// Gets the number of keyed descriptors.
    /// </summary>
    public int KeyedDescriptorCount { get; init; }

    /// <summary>
    /// Gets the number of descriptors backed by factory delegates.
    /// </summary>
    public int FactoryDescriptorCount { get; init; }

    /// <summary>
    /// Gets the number of descriptors backed by implementation instances.
    /// </summary>
    public int InstanceDescriptorCount { get; init; }

    /// <summary>
    /// Gets the number of descriptors rewritten after their original registration.
    /// </summary>
    public int RewrittenDescriptorCount { get; init; }

    /// <summary>
    /// Gets the number of automatic-registration warnings captured in the snapshot.
    /// </summary>
    public int AutoRegistrationWarningCount { get; init; }

    /// <summary>
    /// Gets the number of automatic-registration errors captured in the snapshot.
    /// </summary>
    public int AutoRegistrationErrorCount { get; init; }
}
