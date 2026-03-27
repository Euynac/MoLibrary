using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Features.ObservableInstance;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Modules;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Metadata;
using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.ControlPlane;

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
            RecordState("Long-interval scheduler disabled in configuration", HostedServiceState.Stopped, logLevel: LogLevel.Information);
            return;
        }

        using var periodicTimer = new PeriodicTimer(_options.LongIntervalScanInterval);

        RecordState(
            $"LongIntervalScheduler started. Scan interval: {_options.LongIntervalScanInterval}, Timer threshold: {_options.TimerSafetyThresholdDays} days",
            HostedServiceState.Running,
            logLevel: LogLevel.Information);

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
                RecordState("LongIntervalScheduler scan cancelled", logLevel: LogLevel.Information);
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
            RecordState("No long-interval recurring jobs to scan", logLevel: LogLevel.Debug);
            return 0;
        }

        RecordState(
            $"Found {longIntervalSchedules.Count} long-interval recurring jobs in memory",
            logLevel: LogLevel.Debug);

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
                    logLevel: LogLevel.Information);

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
                RecordState(
                    $"Long-interval recurring job {schedule.JobKey} still has {timeUntilExecution.TotalDays:F1} days until execution, no transition needed",
                    logLevel: LogLevel.Debug);
            }
        }

        if (transitionCount > 0)
        {
            RecordState(
                $"Transitioned {transitionCount} out of {longIntervalSchedules.Count} recurring jobs to Timer mode",
                logLevel: LogLevel.Information);
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
            RecordState("No scheduled instances to scan", logLevel: LogLevel.Debug);
            return 0;
        }

        RecordState(
            $"Found {queryResult.TotalCount} scheduled instances in database",
            logLevel: LogLevel.Debug);

        var timerThreshold = TimeSpan.FromDays(_options.TimerSafetyThresholdDays);
        var transitionCount = 0;

        foreach (var instance in queryResult.Items)
        {
            if (!instance.ScheduledExecutionTime.HasValue)
            {
                RecordState(
                    $"Scheduled instance {instance.InstanceId} (JobKey: {instance.JobKey}) missing ScheduledExecutionTime, skipping",
                    HostedServiceState.Degraded,
                    logLevel: LogLevel.Warning);
                continue;
            }

            var timeUntilExecution = instance.ScheduledExecutionTime.Value - DateTime.UtcNow;

            // If execution time is within threshold, transition to Timer mode
            if (timeUntilExecution <= timerThreshold)
            {
                RecordState(
                    $"Transitioning instance {instance.InstanceId} (JobKey: {instance.JobKey}) to Timer mode. Execution in {timeUntilExecution.TotalHours:F1} hours ({timeUntilExecution.TotalDays:F1} days)",
                    logLevel: LogLevel.Information);

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
                            logLevel: LogLevel.Warning);
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
                RecordState(
                    $"Scheduled instance {instance.InstanceId} still has {timeUntilExecution.TotalDays:F1} days until execution, no transition needed",
                    logLevel: LogLevel.Debug);
            }
        }

        if (transitionCount > 0)
        {
            RecordState(
                $"Transitioned {transitionCount} out of {queryResult.TotalCount} instances to Timer mode",
                logLevel: LogLevel.Information);
        }

        return queryResult.TotalCount;
    }
}
