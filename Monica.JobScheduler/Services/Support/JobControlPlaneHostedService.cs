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
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.Modules;

namespace Monica.JobScheduler.Services.Support;

/// <summary>
/// Repeatedly converges the desired catalog and materializes durable recurring occurrences. Every operation is an
/// idempotent database CAS, so all control-plane replicas may run this loop concurrently.
/// </summary>
internal sealed class JobControlPlaneHostedService(
    IJobSchedulerStore store,
    JobSchedulerHostIdentity identity,
    JobSchedulerRuntimeState runtimeState,
    TimeProvider timeProvider,
    IOptions<ModuleJobSchedulerOption> schedulerOptions,
    IObservableInstanceRegistry observableRegistry,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<JobControlPlaneHostedService> logger)
    : MoBackgroundService(observableRegistry, hostedServiceOptions, serviceScopeFactory, logger)
{
    private readonly ModuleJobSchedulerOption _options = schedulerOptions.Value;
    private DateTimeOffset _nextCleanupUtc;
    private long _synchronizedChangeEpoch;

    public override string? ServiceGroupId => nameof(ModuleJobScheduler);

    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var convergence = await ConvergeAsync(stoppingToken);
                if (convergence.ReadinessChanged)
                {
                    RecordState(
                        convergence.Message,
                        convergence.IsReady ? HostedServiceState.Running : HostedServiceState.Degraded);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _ = runtimeState.SetControlPlane(false, exception.Message);
                RecordState(
                    "Scheduler control-plane convergence failed",
                    HostedServiceState.Degraded,
                    exception,
                    LogLevel.Error);
            }

            await Task.Delay(_options.ControlPlanePollInterval, timeProvider, stoppingToken);
        }
    }

    internal async Task<JobControlPlaneConvergenceResult> ConvergeAsync(CancellationToken cancellationToken)
    {
        var stage = await store.StageReleaseAsync(identity.ReleaseStage, cancellationToken);
        var desiredReleaseId = stage.Version.DesiredReleaseId;
        if (stage.Version.IsTransitionInProgress && desiredReleaseId is not null)
        {
            await store.TryActivateReleaseAsync(
                identity.ReleaseStage.Manifest.SchedulerScopeKey,
                desiredReleaseId,
                cancellationToken);
        }

        var catalog = await store.GetActiveCatalogAsync(
            identity.ReleaseStage.Manifest.SchedulerScopeKey,
            cancellationToken);
        if (catalog is null)
        {
            return CompleteConvergence(
                false,
                desiredReleaseId is null
                    ? "Scheduler catalog has no desired release."
                    : $"Scheduler catalog release '{desiredReleaseId}' is awaiting complete owner publication and activation.");
        }

        if (catalog.Version.IsTransitionInProgress)
        {
            return CompleteConvergence(
                true,
                $"Scheduler control plane remains available on active release " +
                $"'{catalog.Version.ActiveReleaseId}' while desired release " +
                $"'{catalog.Version.DesiredReleaseId}' awaits activation. Admission, claiming, and recurring " +
                "materialization remain durably fenced during the transition.");
        }

        if (_synchronizedChangeEpoch != catalog.Version.ChangeEpoch)
        {
            await SynchronizeRecurringSchedulesAsync(catalog, cancellationToken);
            _synchronizedChangeEpoch = catalog.Version.ChangeEpoch;
        }

        await MaterializeDueOccurrencesAsync(cancellationToken);
        await store.RecoverExpiredLeasesAsync(new ExpiredLeaseRecoveryRequest
        {
            SchedulerScopeKey = catalog.Version.SchedulerScopeKey,
            MaxCount = _options.MaxExpiredLeaseRecoveriesPerCycle
        }, cancellationToken);
        await CleanupHistoryAsync(catalog, cancellationToken);
        return CompleteConvergence(
            true,
            $"Scheduler catalog release '{catalog.Version.ActiveReleaseId}' is active and stable.");
    }

    private JobControlPlaneConvergenceResult CompleteConvergence(bool isReady, string message)
    {
        var readinessChanged = runtimeState.SetControlPlane(isReady, message);
        return new JobControlPlaneConvergenceResult(isReady, message, readinessChanged);
    }

    private async Task SynchronizeRecurringSchedulesAsync(
        JobCatalogSnapshot catalog,
        CancellationToken cancellationToken)
    {
        foreach (var definition in catalog.Definitions.Where(static definition =>
                     definition.Declaration.JobType == JobType.Recurring))
        {
            var declaration = definition.Declaration;
            var schedule = new RecurringScheduleDefinition
            {
                CronExpression = declaration.CronExpression!,
                TimeZoneId = declaration.TimeZoneId!,
                StartTimeUtc = declaration.StartTimeUtc,
                EndTimeUtc = declaration.EndTimeUtc
            };
            var suspended = _options.RecurringJobDebugMode || definition.IsDisabled;
            await store.SynchronizeRecurringScheduleAsync(new RecurringScheduleSynchronization
            {
                Template = definition.CreateExecutionTemplate(),
                Schedule = schedule,
                ChangeEpoch = catalog.Version.ChangeEpoch,
                IsSuspended = suspended
            }, cancellationToken);
        }
    }

    private async Task MaterializeDueOccurrencesAsync(CancellationToken cancellationToken)
    {
        var scope = identity.ReleaseStage.Manifest.SchedulerScopeKey;
        var remainingBudget = _options.MaxRecurringMaterializationsPerCycle;
        while (remainingBudget > 0)
        {
            var cursors = await store.GetDueRecurringSchedulesAsync(
                scope,
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

                var instanceId = CreateRecurringInstanceId(cursor.Key, occurrence);
                var result = await store.TryMaterializeRecurringOccurrenceAsync(
                    new RecurringOccurrenceMaterialization
                    {
                        CursorKey = cursor.Key,
                        ExpectedVersion = cursor.Version,
                        ExpectedOccurrenceUtc = occurrence,
                        NextOccurrenceUtc = cursor.Schedule.GetNextOccurrence(occurrence.AddTicks(1)),
                        InstanceId = instanceId
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
            // against stale snapshots, so leave the refreshed durable cursors for the next convergence cycle.
            if (materializedThisRound == 0)
            {
                return;
            }
        }
    }

    private async Task CleanupHistoryAsync(
        JobCatalogSnapshot catalog,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!_options.EnableHistoryCleanup || now < _nextCleanupUtc)
        {
            return;
        }

        var policies = catalog.Definitions.ToDictionary(
            static definition => definition.Declaration.JobKey,
            static definition => new JobHistoryRetentionPolicy
            {
                MaxRecords = definition.Policy.MaxRetainedHistoryRecords ?? 0,
                MaxDays = definition.Policy.MaxRetentionDays
            },
            StringComparer.Ordinal);
        var candidates = await store.GetExecutionCleanupCandidatesAsync(
            catalog.Version.SchedulerScopeKey,
            policies,
            _options.MaxRetainedOrphanedExecutions,
            _options.MaxHistoryDeletionsPerCycle,
            cancellationToken);
        await store.DeleteExecutionsAsync(
            catalog.Version.SchedulerScopeKey,
            candidates,
            cancellationToken);
        // A full batch proves more eligible history may remain. Drain another bounded batch on the next control-plane
        // poll; use the configured interval only after a partial batch establishes that the current backlog is drained.
        _nextCleanupUtc = candidates.Count == _options.MaxHistoryDeletionsPerCycle
            ? now
            : now.Add(_options.HistoryCleanupInterval);
    }

    private static string CreateRecurringInstanceId(
        RecurringScheduleCursorKey key,
        DateTimeOffset occurrence)
    {
        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{key.SchedulerScopeKey}|{key.ActivationEpoch}|{key.JobRevisionId}|{occurrence.UtcTicks}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}

/// <summary>
/// Describes whether one control-plane cycle reached a stable active catalog.
/// </summary>
internal readonly record struct JobControlPlaneConvergenceResult(
    bool IsReady,
    string Message,
    bool ReadinessChanged);
