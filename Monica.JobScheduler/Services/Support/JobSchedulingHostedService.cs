using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Definitions;
using Monica.JobScheduler.Models.Execution;
using Monica.Modules;

namespace Monica.JobScheduler.Services.Support;

/// <summary>
/// Schedules this host's own discovered jobs: it publishes the local owner snapshot, materializes due recurring
/// occurrences, recovers expired execution leases, and cleans up terminal history. Every operation is an idempotent
/// database compare-and-swap, so all replicas of one owner may run this loop concurrently, and every host may run it
/// without any deployment coordination.
/// </summary>
internal sealed class JobSchedulingHostedService(
    IJobSchedulerStore store,
    IReadOnlyList<LocalJobDefinition> localDefinitions,
    JobSchedulerRuntimeState runtimeState,
    TimeProvider timeProvider,
    IOptions<ModuleJobSchedulerOption> schedulerOptions,
    IObservableInstanceRegistry observableRegistry,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<JobSchedulingHostedService> logger)
    : MoBackgroundService(observableRegistry, hostedServiceOptions, serviceScopeFactory, logger)
{
    private readonly ModuleJobSchedulerOption _options = schedulerOptions.Value;
    private readonly bool _hasLocalRecurringJobs = localDefinitions.Any(
        static definition => definition.Declaration.JobType == JobType.Recurring);
    private DateTimeOffset _nextSnapshotSyncUtc;
    private DateTimeOffset _nextCleanupUtc;

    public override string? ServiceGroupId => nameof(ModuleJobScheduler);

    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScheduleAsync(stoppingToken);
                if (runtimeState.SetScheduling(
                        true,
                        $"Scheduling is active for {_options.GetProjectName()} with {localDefinitions.Count} local job(s)."))
                {
                    RecordState(
                        $"Scheduler active for owner '{_options.GetProjectName()}'",
                        HostedServiceState.Running);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _ = runtimeState.SetScheduling(false, exception.Message);
                RecordState(
                    "Scheduler scheduling cycle failed",
                    HostedServiceState.Degraded,
                    exception,
                    LogLevel.Error);
            }

            await Task.Delay(_options.SchedulingPollInterval, timeProvider, stoppingToken);
        }
    }

    internal async Task ScheduleAsync(CancellationToken cancellationToken)
    {
        var scopeKey = _options.SchedulerScopeKey;
        var ownerKey = _options.GetProjectName();
        await SyncOwnerStateAsync(scopeKey, ownerKey, cancellationToken);
        await store.RecoverExpiredLeasesAsync(new ExpiredLeaseRecoveryRequest
        {
            SchedulerScopeKey = scopeKey,
            MaxCount = _options.MaxExpiredLeaseRecoveriesPerCycle
        }, cancellationToken);
        await MaterializeDueOccurrencesAsync(scopeKey, ownerKey, cancellationToken);
        await CleanupHistoryAsync(scopeKey, cancellationToken);
    }

    private async Task SyncOwnerStateAsync(string scopeKey, string ownerKey, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (now < _nextSnapshotSyncUtc)
        {
            return;
        }

        await store.SyncOwnerSnapshotAsync(new JobOwnerSnapshot(
            scopeKey,
            ownerKey,
            localDefinitions.Select(static definition => definition.Declaration).ToArray()), cancellationToken);

        // Reconcile host-owned suspension reasons with the same cadence as the owner snapshot. Policy edits update
        // their cursor synchronously; this pass is only needed at startup and after a definition snapshot changes.
        if (_hasLocalRecurringJobs)
        {
            var hostReasons = _options.RecurringJobDebugMode
                ? JobRecurringScheduleSuspensionReason.DebugMode
                : JobRecurringScheduleSuspensionReason.None;
            foreach (var definition in localDefinitions.Where(static definition =>
                         definition.Declaration.JobType == JobType.Recurring))
            {
                await store.SynchronizeRecurringScheduleAsync(new RecurringScheduleSynchronization
                {
                    CursorKey = new RecurringScheduleCursorKey
                    {
                        SchedulerScopeKey = scopeKey,
                        OwnerKey = ownerKey,
                        JobKey = definition.Declaration.JobKey
                    },
                    HostSuspensionReasons = hostReasons
                }, cancellationToken);
            }
        }

        // Advance the cadence only after both the snapshot and every cursor synchronization succeeds. A partial
        // failure must retry the complete owner synchronization on the next scheduling cycle.
        _nextSnapshotSyncUtc = timeProvider.GetUtcNow().Add(_options.SnapshotSyncInterval);
    }

    private async Task MaterializeDueOccurrencesAsync(string scopeKey, string ownerKey, CancellationToken cancellationToken)
    {
        if (_options.RecurringJobDebugMode)
        {
            return;
        }

        var remainingBudget = _options.MaxRecurringMaterializationsPerCycle;
        while (remainingBudget > 0)
        {
            var cursors = await store.GetDueRecurringSchedulesAsync(
                scopeKey,
                ownerKey,
                remainingBudget,
                cancellationToken);
            if (cursors.Count == 0)
            {
                return;
            }

            var materializedThisRound = 0;
            foreach (var cursor in cursors)
            {
                if (cursor.NextOccurrenceUtc is not { } occurrence)
                {
                    continue;
                }

                // The store owns the authoritative clock and advances the cursor past all missed occurrences in the
                // same atomic operation as enqueueing this one. The host must not calculate the next cursor value from
                // a potentially skewed process clock.
                var result = await store.TryMaterializeRecurringOccurrenceAsync(
                    new RecurringOccurrenceMaterialization
                    {
                        CursorKey = cursor.Key,
                        ExpectedVersion = cursor.Version,
                        ExpectedOccurrenceUtc = occurrence,
                        InstanceId = CreateRecurringInstanceId(cursor.Key, occurrence)
                    },
                    cancellationToken);
                if (result.Status != RecurringMaterializationStatus.Materialized)
                {
                    continue;
                }

                remainingBudget--;
                materializedThisRound++;
                if (remainingBudget == 0)
                {
                    return;
                }
            }

            // Another replica may have advanced every observed cursor. Re-querying without local progress would spin
            // against stale snapshots, so leave the refreshed durable cursors for the next scheduling cycle.
            if (materializedThisRound == 0)
            {
                return;
            }
        }
    }

    private async Task CleanupHistoryAsync(string scopeKey, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!_options.EnableHistoryCleanup || now < _nextCleanupUtc)
        {
            return;
        }

        var policies = new Dictionary<JobId, JobHistoryRetentionPolicy>();
        var pageNumber = 1;
        while (true)
        {
            var page = await store.QueryDefinitionsAsync(
                scopeKey,
                new JobDefinitionQuery { PageNumber = pageNumber, PageSize = 500 },
                cancellationToken);
            foreach (var definition in page.Items)
            {
                var effective = definition.EffectiveConfiguration;
                policies[new JobId(definition.OwnerKey, definition.Declaration.JobKey)] = new JobHistoryRetentionPolicy
                {
                    MaxRecords = effective.MaxRetainedHistoryRecords,
                    MaxDays = effective.MaxRetentionDays
                };
            }

            if (page.Items.Count < 500)
            {
                break;
            }

            pageNumber++;
        }

        var candidates = await store.GetExecutionCleanupCandidatesAsync(
            scopeKey,
            policies,
            _options.MaxRetainedOrphanedExecutions,
            _options.MaxHistoryDeletionsPerCycle,
            cancellationToken);
        await store.DeleteExecutionsAsync(scopeKey, candidates, cancellationToken);
        // A full batch proves more eligible history may remain. Drain another bounded batch on the next scheduling
        // cycle; use the configured interval only after a partial batch establishes that the backlog is drained.
        _nextCleanupUtc = candidates.Count == _options.MaxHistoryDeletionsPerCycle
            ? now
            : now.Add(_options.HistoryCleanupInterval);
    }

    internal static string CreateRecurringInstanceId(
        RecurringScheduleCursorKey key,
        DateTimeOffset occurrence)
    {
        var payload = new StringBuilder()
            .AppendLengthPrefixed(key.SchedulerScopeKey)
            .AppendLengthPrefixed(key.OwnerKey)
            .AppendLengthPrefixed(key.JobKey)
            .Append('|')
            .Append(occurrence.UtcTicks.ToString(CultureInfo.InvariantCulture))
            .ToString();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}

file static class StringBuilderExtensions
{
    public static StringBuilder AppendLengthPrefixed(this StringBuilder builder, string value)
    {
        return builder.Append(value.Length).Append(':').Append(value);
    }
}
