using Monica.JobScheduler.Models.Catalog;

namespace Monica.JobScheduler.Models;

/// <summary>
/// Binds one immutable code declaration to the CLR types available in the current worker process.
/// </summary>
/// <remarks>
/// This model is process-local and is never persisted. Durable catalog and execution records use stable string
/// identities so control-plane hosts never need to load worker assemblies.
/// </remarks>
public sealed record LocalJobDefinition
{
    /// <summary>
    /// Gets the immutable declaration discovered from the worker assembly.
    /// </summary>
    public required JobDeclaration Declaration { get; init; }

    /// <summary>
    /// Gets the concrete job implementation type resolved from the worker dependency-injection scope.
    /// </summary>
    public required Type JobClrType { get; init; }

    /// <summary>
    /// Gets the triggered-job argument type, or <see langword="null"/> for a recurring job.
    /// </summary>
    public Type? JobArgsClrType { get; init; }
}
