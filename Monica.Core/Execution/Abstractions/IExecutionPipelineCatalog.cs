using Monica.Core.Execution.Models;

namespace Monica.Core.Execution.Abstractions;

/// <summary>
/// Provides a host-local, read-only view of registered execution behaviors and observed immutable plans.
/// </summary>
/// <remarks>
/// Snapshot queries never resolve behavior services or evaluate descriptor filters. Exact plans appear only after an
/// operation executes or <see cref="InspectPlan"/> explicitly materializes its descriptor. Catalog state is retained
/// for the lifetime of the host and does not contain invocation inputs, targets, principals, or feature values.
/// </remarks>
public interface IExecutionPipelineCatalog
{
    /// <summary>
    /// Gets all registered behaviors and every plan observed by the current host.
    /// </summary>
    /// <returns>An immutable point-in-time catalog snapshot.</returns>
    ExecutionPipelineCatalogSnapshot GetSnapshot();

    /// <summary>
    /// Materializes and caches the exact plan for a descriptor without resolving behavior instances.
    /// </summary>
    /// <param name="descriptor">The reusable descriptor to inspect.</param>
    /// <returns>
    /// The ready or faulted plan snapshot. Descriptor-filter failures are represented in the returned snapshot and
    /// remain cached so a later execution deterministically rethrows the same failure.
    /// </returns>
    ExecutionPipelinePlanSnapshot InspectPlan(ExecutionDescriptor descriptor);
}
