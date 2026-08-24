using Microsoft.Extensions.Options;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models.Execution;
using Monica.Modules;

namespace Monica.JobScheduler.Providers;

/// <summary>
/// Volatile implementation of the unified scheduler store for development and deterministic tests.
/// </summary>
/// <remarks>
/// Every catalog and execution mutation shares one synchronization boundary so compound operations have the same
/// atomicity contract as a relational implementation.
/// </remarks>
public sealed partial class InMemoryJobSchedulerStore(
    TimeProvider timeProvider,
    IOptions<ModuleJobSchedulerOption>? options = null) : IJobSchedulerStore
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly JobExecutionHistoryLimits _historyLimits = new(
        options?.Value.MaxExecutionHistoryEntriesPerExecution
        ?? JobExecutionHistoryLimits.DEFAULT_MAX_ENTRIES,
        options?.Value.MaxExecutionHistoryMessageLength
        ?? JobExecutionHistoryLimits.DEFAULT_MAX_MESSAGE_LENGTH);
    private readonly Dictionary<string, CatalogScopeState> _catalogScopes = new(StringComparer.Ordinal);

    private bool IsStableActivation(string schedulerScopeKey, long activationEpoch)
    {
        return _catalogScopes.TryGetValue(schedulerScopeKey, out var scope)
               && scope.ActiveReleaseId is not null
               && scope.ActivationEpoch == activationEpoch
               && scope.ActiveIntentEpoch == scope.DesiredIntentEpoch;
    }
}
