using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Execution;
using Monica.Modules;

namespace Monica.JobScheduler.Services.Support;

/// <summary>
/// Claims this host's own owner's queued work and executes it under fenced leases with timeout, cooperative
/// cancellation, and retry handling. No broker delivery or process-local execution projection participates in
/// correctness.
/// </summary>
internal sealed class JobExecutionWorkerHostedService(
    IJobSchedulerStore store,
    IReadOnlyList<LocalJobDefinition> localDefinitions,
    JobOrchestrator orchestrator,
    JobSchedulerRuntimeState runtimeState,
    TimeProvider timeProvider,
    IHostApplicationLifetime applicationLifetime,
    IOptions<ModuleJobSchedulerOption> schedulerOptions,
    IObservableInstanceRegistry observableRegistry,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<JobExecutionWorkerHostedService> logger)
    : MoBackgroundService(observableRegistry, hostedServiceOptions, serviceScopeFactory, logger)
{
    private readonly ConcurrentDictionary<string, Task> _executions = new(StringComparer.Ordinal);
    private readonly ModuleJobSchedulerOption _options = schedulerOptions.Value;
    private readonly CancellationTokenSource _workerStop = new();

    public override string? ServiceGroupId => nameof(ModuleJobScheduler);

    public override void Dispose()
    {
        _workerStop.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        using var linkedStop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _workerStop.Token);
        var workerToken = linkedStop.Token;
        while (!workerToken.IsCancellationRequested)
        {
            try
            {
                ObserveCompletedExecutions();
                await ClaimAvailableWorkAsync(workerToken);
                if (runtimeState.SetWorker(
                        true,
                        $"Worker is active for {_options.GetProjectName()} with {localDefinitions.Count} local job(s)."))
                {
                    RecordState(
                        $"Worker active with {_executions.Count} in-flight execution(s)",
                        HostedServiceState.Running);
                }
            }
            catch (OperationCanceledException) when (workerToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _ = runtimeState.SetWorker(false, exception.Message);
                RecordState(
                    "Scheduler worker cycle failed",
                    HostedServiceState.Degraded,
                    exception,
                    LogLevel.Error);
            }

            await Task.Delay(_options.WorkerPollInterval, timeProvider, workerToken);
        }
    }

    protected override Task OnStoppingAsync(CancellationToken cancellationToken)
    {
        _workerStop.Cancel();
        _ = runtimeState.SetWorker(false, "Worker is stopping.");
        return Task.CompletedTask;
    }

    protected override async Task OnStoppedAsync(CancellationToken cancellationToken)
    {
        var tasks = _executions.Values.ToArray();
        if (tasks.Length != 0)
        {
            try
            {
                await Task.WhenAll(tasks).WaitAsync(_options.WorkerShutdownGracePeriod, cancellationToken);
            }
            catch (TimeoutException)
            {
                RecordState(
                    $"{_executions.Count} execution(s) outlived the worker shutdown grace period",
                    HostedServiceState.Degraded,
                    logLevel: LogLevel.Warning);
                // Host shutdown is not a logical job cancellation. The linked execution token has already been
                // cancelled; a cooperative attempt releases its lease and requeues, while an uncooperative attempt
                // remains fenced until its lease expires and the scheduling plane recovers it.
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                RecordState(
                    "One or more scheduler executions faulted during worker shutdown",
                    HostedServiceState.Degraded,
                    exception,
                    LogLevel.Error);
            }
            finally
            {
                ObserveCompletedExecutions();
            }
        }
    }

    internal async Task ClaimAvailableWorkAsync(CancellationToken workerToken)
    {
        var availableSlots = _options.MaxWorkerExecutionThreads - _executions.Count;
        if (availableSlots <= 0 || localDefinitions.Count == 0)
        {
            return;
        }

        var leases = await store.ClaimAsync(new JobClaimRequest
        {
            SchedulerScopeKey = _options.SchedulerScopeKey,
            OwnerKey = _options.GetProjectName(),
            WorkerInstanceId = _options.WorkerInstanceId,
            JobKeys = localDefinitions.Select(static definition => definition.Declaration.JobKey).ToArray(),
            LeaseDuration = _options.ExecutionLeaseDuration,
            MaxCount = Math.Min(availableSlots, _options.MaxClaimBatchSize)
        }, workerToken);
        foreach (var lease in leases)
        {
            var task = ExecuteLeaseAsync(lease, workerToken);
            if (!_executions.TryAdd(lease.Execution.InstanceId, task))
            {
                throw new InvalidOperationException(
                    $"Execution '{lease.Execution.InstanceId}' was claimed twice by one worker.");
            }
        }
    }

    internal async Task ExecuteLeaseAsync(JobExecutionLease lease, CancellationToken workerToken)
    {
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(workerToken);
        var cancellationRequested = false;
        var timedOut = false;
        var executionTask = orchestrator.ExecuteAsync(lease, executionCancellation.Token);
        using var renewalStop = new CancellationTokenSource();
        var timeoutSignal = Task.Delay(
            lease.Execution.Template.MaxExecutionTimeout,
            timeProvider,
            renewalStop.Token);

        try
        {
            while (!executionTask.IsCompleted)
            {
                var renewalDelay = Task.Delay(
                    _options.ExecutionLeaseRenewInterval,
                    timeProvider,
                    renewalStop.Token);
                var completed = await Task.WhenAny(executionTask, renewalDelay, timeoutSignal);
                if (completed == executionTask)
                {
                    await renewalStop.CancelAsync();
                    try
                    {
                        await renewalDelay;
                    }
                    catch (OperationCanceledException) when (renewalStop.IsCancellationRequested)
                    {
                    }
                    break;
                }

                if (completed == timeoutSignal)
                {
                    timedOut = true;
                    if (!await CancelAndAwaitExecutionAsync(
                            executionCancellation,
                            executionTask,
                            lease.Execution.InstanceId,
                            "the configured execution timeout"))
                    {
                        RequestWorkerRestart(
                            lease.Execution.InstanceId,
                            "Job code ignored cancellation after its execution timeout.");
                        return;
                    }

                    break;
                }

                var renewal = await store.RenewLeaseAsync(
                    lease.LeaseKey,
                    _options.ExecutionLeaseDuration,
                    CancellationToken.None);
                if (renewal.Status == JobLeaseRenewalStatus.Lost)
                {
                    if (!await CancelAndAwaitExecutionAsync(
                            executionCancellation,
                            executionTask,
                            lease.Execution.InstanceId,
                            "execution lease loss"))
                    {
                        RequestWorkerRestart(
                            lease.Execution.InstanceId,
                            "Job code ignored cancellation after its execution lease was lost.");
                    }

                    return;
                }

                if (timedOut)
                {
                    if (!await CancelAndAwaitExecutionAsync(
                            executionCancellation,
                            executionTask,
                            lease.Execution.InstanceId,
                            "the configured execution timeout"))
                    {
                        RequestWorkerRestart(
                            lease.Execution.InstanceId,
                            "Job code ignored cancellation after its execution timeout.");
                        return;
                    }

                    break;
                }

                if (renewal.Status == JobLeaseRenewalStatus.CancellationRequested)
                {
                    cancellationRequested = true;
                    if (!await CancelAndAwaitExecutionAsync(
                            executionCancellation,
                            executionTask,
                            lease.Execution.InstanceId,
                            "a durable cancellation request"))
                    {
                        RequestWorkerRestart(
                            lease.Execution.InstanceId,
                            "Job code ignored a durable cancellation request.");
                        return;
                    }

                    break;
                }
            }
        }
        catch
        {
            if (!await CancelAndAwaitExecutionAsync(
                    executionCancellation,
                    executionTask,
                    lease.Execution.InstanceId,
                    "a worker execution-loop failure"))
            {
                RequestWorkerRestart(
                    lease.Execution.InstanceId,
                    "Job code ignored cancellation after the worker execution loop failed.");
            }

            throw;
        }
        finally
        {
            await renewalStop.CancelAsync();
        }

        var result = await executionTask;
        if (workerToken.IsCancellationRequested)
        {
            await store.ReleaseLeaseAsync(lease.LeaseKey, CancellationToken.None);
            return;
        }

        var outcome = timedOut
            ? JobAttemptOutcome.Failed
            : cancellationRequested
                ? JobAttemptOutcome.Cancelled
                : result.Outcome;
        var message = timedOut
            ? $"Execution timed out after {lease.Execution.Template.MaxExecutionTimeout}"
            : result.Message;
        await store.CompleteAttemptAsync(new JobAttemptCompletion
        {
            LeaseKey = lease.LeaseKey,
            Outcome = outcome,
            Message = message,
            RetryDelay = _options.ExecutionRetryDelay
        }, CancellationToken.None);
    }

    private async Task<bool> CancelAndAwaitExecutionAsync(
        CancellationTokenSource executionCancellation,
        Task executionTask,
        string instanceId,
        string cancellationReason)
    {
        try
        {
            await executionCancellation.CancelAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Execution {InstanceId} cancellation callbacks failed", instanceId);
        }

        var cancellationDeadline = Task.Delay(
            _options.ExecutionCancellationGracePeriod,
            timeProvider,
            CancellationToken.None);
        if (await Task.WhenAny(executionTask, cancellationDeadline) != executionTask)
        {
            logger.LogCritical(
                "Execution {InstanceId} did not exit within {GracePeriod} after {CancellationReason}",
                instanceId,
                _options.ExecutionCancellationGracePeriod,
                cancellationReason);
            return false;
        }

        try
        {
            await executionTask;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Execution {InstanceId} faulted while exiting after cancellation", instanceId);
        }

        return true;
    }

    private void RequestWorkerRestart(string instanceId, string reason)
    {
        var message = $"Worker restart requested because execution '{instanceId}' did not stop safely. {reason}";
        _ = runtimeState.SetWorker(false, message);
        RecordState(message, HostedServiceState.Faulted, logLevel: LogLevel.Critical);
        applicationLifetime.StopApplication();
    }

    private void ObserveCompletedExecutions()
    {
        foreach (var pair in _executions.Where(static pair => pair.Value.IsCompleted).ToArray())
        {
            if (_executions.TryRemove(pair.Key, out var task) && task.IsFaulted)
            {
                RecordState(
                    $"Worker execution '{pair.Key}' pipeline faulted",
                    HostedServiceState.Degraded,
                    task.Exception,
                    LogLevel.Error);
            }
        }
    }
}
