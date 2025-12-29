using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.HostedServices;
using MoLibrary.Core.Features.HostedServices.Models;
using MoLibrary.Core.Features.ObservableInstance;
using MoLibrary.Core.Modules;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Metadata;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.Modules;

namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Background service for managing long-interval jobs that exceed Timer capacity.
/// Periodically scans:
/// 1. In-memory long-interval recurring job schedules
/// 2. Database Scheduled state triggered job instances
/// Converts them to Timer mode when close to execution time.
/// </summary>
public class LongIntervalSchedulerService(
    RecurringJobScheduler recurringJobScheduler,
    TriggeredJobScheduler triggeredJobScheduler,
    IJobDefinitionCacheService cacheService,
    IMoJobMetadataRepository metadataRepository,
    IOptions<ModuleJobSchedulerOption> options,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    ILogger<LongIntervalSchedulerService> logger)
    : MoBackgroundService(observableManager, hostedServiceOptions, logger)
{
    private readonly ModuleJobSchedulerOption _options = options.Value;

    public override string ServiceName => nameof(LongIntervalSchedulerService);

    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableLongIntervalScheduler)
        {
            RecordState("Long-interval scheduler disabled in configuration", HostedServiceState.Stopped);
            logger.LogInformation("LongIntervalScheduler is disabled, exiting");
            return;
        }

        using var periodicTimer = new PeriodicTimer(_options.LongIntervalScanInterval);

        logger.LogInformation(
            "LongIntervalScheduler started. Scan interval: {Interval}, Timer threshold: {Threshold} days",
            _options.LongIntervalScanInterval,
            _options.TimerSafetyThresholdDays);

        RecordState("Long-interval scheduler started", HostedServiceState.Running);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await periodicTimer.WaitForNextTickAsync(stoppingToken);

                RecordState("Scanning long-interval jobs", HostedServiceState.Executing);

                var (recurringCount, instanceCount) = await ScanAndTransitionJobsAsync(stoppingToken);

                RecordState(
                    $"Scan completed: {recurringCount} recurring jobs, {instanceCount} instances scanned",
                    HostedServiceState.Running);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("LongIntervalScheduler scan cancelled");
                break;
            }
            catch (Exception ex)
            {
                RecordState("Scan failed", HostedServiceState.Degraded, ex);
                // Continue running, wait for next scan
            }
        }

        RecordState("Long-interval scheduler stopping", HostedServiceState.Stopping);
    }

    private async Task<(int RecurringCount, int InstanceCount)> ScanAndTransitionJobsAsync(CancellationToken cancellationToken)
    {
        var recurringCount = await ScanRecurringJobsAsync(cancellationToken);
        var instanceCount = await ScanScheduledInstancesAsync(cancellationToken);
        return (recurringCount, instanceCount);
    }

    private async Task<int> ScanRecurringJobsAsync(CancellationToken cancellationToken)
    {
        // Get long-interval jobs from RecurringJobScheduler's in-memory dictionary
        var longIntervalSchedules = recurringJobScheduler.GetLongIntervalSchedules();

        if (longIntervalSchedules.Count == 0)
        {
            RecordState("No long-interval recurring jobs to scan", givenLogLevel: LogLevel.Debug);
            return 0;
        }

        RecordState(
            $"Found {longIntervalSchedules.Count} long-interval recurring jobs in memory",
            givenLogLevel: LogLevel.Debug);

        var timerThreshold = TimeSpan.FromDays(_options.TimerSafetyThresholdDays);
        var transitionCount = 0;

        foreach (var schedule in longIntervalSchedules)
        {
            var timeUntilExecution = schedule.NextScheduledTime - DateTime.UtcNow;

            // If execution time is within threshold, transition to Timer mode
            if (timeUntilExecution <= timerThreshold)
            {
                RecordState(
                    $"Transitioning recurring job {schedule.JobKey} to Timer mode. Execution in {timeUntilExecution.TotalHours:F1} hours ({timeUntilExecution.TotalDays:F1} days)",
                    givenLogLevel: LogLevel.Information);

                try
                {
                    await recurringJobScheduler.TransitionToTimerModeAsync(schedule.JobKey, cancellationToken);
                    transitionCount++;
                }
                catch (Exception ex)
                {
                    RecordState(
                        $"Failed to transition recurring job {schedule.JobKey} to Timer mode",
                        exception: ex);
                }
            }
            else
            {
                // Keep logger for detailed debug info - doesn't need state tracking
                logger.LogDebug(
                    "Long-interval recurring job {JobKey} still has {Days} days until execution, no transition needed",
                    schedule.JobKey,
                    timeUntilExecution.TotalDays);
            }
        }

        if (transitionCount > 0)
        {
            RecordState(
                $"Transitioned {transitionCount} out of {longIntervalSchedules.Count} recurring jobs to Timer mode",
                givenLogLevel: LogLevel.Information);
        }

        return longIntervalSchedules.Count;
    }

    private async Task<int> ScanScheduledInstancesAsync(CancellationToken cancellationToken)
    {
        // Query Scheduled state Job Instances (TriggeredJob)
        var query = new JobInstanceQuery
        {
            State = JobState.Scheduled,
            PageSize = 1000
        };

        var queryResult = await metadataRepository.QueryInstancesAsync(query, cancellationToken);

        if (queryResult.TotalCount == 0)
        {
            RecordState("No scheduled instances to scan", givenLogLevel: LogLevel.Debug);
            return 0;
        }

        RecordState(
            $"Found {queryResult.TotalCount} scheduled instances in database",
            givenLogLevel: LogLevel.Debug);

        var timerThreshold = TimeSpan.FromDays(_options.TimerSafetyThresholdDays);
        var transitionCount = 0;

        foreach (var instance in queryResult.Items)
        {
            if (!instance.ScheduledExecutionTime.HasValue)
            {
                RecordState(
                    $"Scheduled instance {instance.InstanceId} (JobKey: {instance.JobKey}) missing ScheduledExecutionTime, skipping",
                    HostedServiceState.Degraded,
                    givenLogLevel: LogLevel.Warning);
                continue;
            }

            var timeUntilExecution = instance.ScheduledExecutionTime.Value - DateTime.UtcNow;

            // If execution time is within threshold, transition to Timer mode
            if (timeUntilExecution <= timerThreshold)
            {
                RecordState(
                    $"Transitioning instance {instance.InstanceId} (JobKey: {instance.JobKey}) to Timer mode. Execution in {timeUntilExecution.TotalHours:F1} hours ({timeUntilExecution.TotalDays:F1} days)",
                    givenLogLevel: LogLevel.Information);

                try
                {
                    var definition = await cacheService.GetDefinitionAsync(instance.JobKey, cancellationToken);
                    if (definition != null)
                    {
                        // Call TriggeredJobScheduler to reschedule (will check interval and create Timer)
                        triggeredJobScheduler.ScheduleDelayedJob(instance, definition, instance.ScheduledExecutionTime.Value);
                        transitionCount++;
                    }
                    else
                    {
                        RecordState(
                            $"Definition not found for instance {instance.InstanceId} (JobKey: {instance.JobKey}), skipping transition",
                            HostedServiceState.Degraded,
                            givenLogLevel: LogLevel.Warning);
                    }
                }
                catch (Exception ex)
                {
                    RecordState(
                        $"Failed to transition instance {instance.InstanceId} to Timer mode",
                        exception: ex);
                }
            }
            else
            {
                // Keep logger for detailed debug info - doesn't need state tracking
                logger.LogDebug(
                    "Scheduled instance {InstanceId} still has {Days} days until execution, no transition needed",
                    instance.InstanceId,
                    timeUntilExecution.TotalDays);
            }
        }

        if (transitionCount > 0)
        {
            RecordState(
                $"Transitioned {transitionCount} out of {queryResult.TotalCount} instances to Timer mode",
                givenLogLevel: LogLevel.Information);
        }

        return queryResult.TotalCount;
    }
}
