namespace Monica.JobScheduler.Abstractions;

/// <summary>
/// Describes the durable storage behind an <see cref="IJobSchedulerStore"/> implementation for operational
/// display. Implementing this interface is optional; consumers fall back to the store's concrete type name.
/// </summary>
public interface IJobSchedulerStoreDescriptor
{
    /// <summary>
    /// Gets the human-readable storage family name, for example "InMemory" or "EF Core".
    /// </summary>
    string StoreKind { get; }

    /// <summary>
    /// Gets the optional storage provider name, for example the Entity Framework provider identifier.
    /// Returns <see langword="null"/> when the store has no provider distinction to report.
    /// </summary>
    string? Provider { get; }
}
