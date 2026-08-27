using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Extensions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Execution;
using Monica.Modules;

namespace Monica.JobScheduler.Services;

/// <summary>
/// Runs one claimed job attempt in its own asynchronous dependency-injection scope and reports only the user-code
/// outcome. The durable store remains the sole owner of lifecycle, retries, leases, and fencing.
/// </summary>
public sealed class JobOrchestrator(
    IServiceScopeFactory serviceScopeFactory,
    JobExecutor jobExecutor,
    JobRegistry jobRegistry,
    IJobSchedulerStore store,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger<JobOrchestrator> logger)
{
    private readonly JsonSerializerOptions _serializerOptions = options.Value.JobArgsSerializerOptions;

    internal async Task<JobAttemptResult> ExecuteAsync(
        JobExecutionLease lease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var execution = lease.Execution;

        try
        {
            var jobType = jobRegistry.GetJobClrType(execution.Template.JobKey)
                           ?? throw new InvalidOperationException(
                               $"Local worker does not contain job '{execution.Template.JobKey}'.");
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var arguments = DeserializeArguments(execution);
            var context = new JobExecutionContext
            {
                InstanceId = execution.InstanceId,
                ServiceProvider = scope.ServiceProvider,
                JobType = jobType,
                JobArgs = arguments,
                CancellationToken = cancellationToken,
                WriteExecutionLogAsync = (message, level, exception, token) => WriteLogAsync(
                    lease.LeaseKey,
                    message,
                    level,
                    exception,
                    token)
            };

            if (execution.Template.JobType == JobType.Recurring)
            {
                await jobExecutor.ExecuteRecurringJobAsync(context);
            }
            else
            {
                await jobExecutor.ExecuteTriggeredJobAsync(context);
            }

            return new JobAttemptResult(JobAttemptOutcome.Succeeded, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new JobAttemptResult(JobAttemptOutcome.Cancelled, "Execution observed cancellation");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Job {JobKey} instance {InstanceId} failed: {Message}",
                execution.Template.JobKey,
                execution.InstanceId,
                exception.GetMessageRecursively());
            return new JobAttemptResult(JobAttemptOutcome.Failed, exception.ToString());
        }
    }

    private object? DeserializeArguments(JobExecutionInstance execution)
    {
        if (execution.Template.JobType == JobType.Recurring)
        {
            return null;
        }

        var argumentsKey = execution.Template.JobArgsKey
                           ?? throw new InvalidOperationException("Triggered execution has no argument identity.");
        var argumentsType = jobRegistry.GetJobArgsClrType(argumentsKey)
                            ?? throw new InvalidOperationException(
                                $"Local worker does not contain argument type '{argumentsKey}'.");
        return JsonSerializer.Deserialize(execution.JobArgs!, argumentsType, _serializerOptions)
               ?? throw new InvalidOperationException(
                   $"Triggered job arguments for '{execution.InstanceId}' deserialized to null.");
    }

    private async Task WriteLogAsync(
        JobLeaseKey leaseKey,
        string message,
        LogLevel level,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        var content = exception is null ? message : $"{message}{Environment.NewLine}{exception}";
        var status = await store.AppendExecutionLogAsync(new JobExecutionLogEntry
        {
            LeaseKey = leaseKey,
            Message = content,
            LogLevel = level
        }, cancellationToken);
        if (status == JobLeaseMutationStatus.Lost)
        {
            throw new InvalidOperationException("The execution lease was lost before the log entry could be persisted.");
        }
    }
}

internal sealed record JobAttemptResult(JobAttemptOutcome Outcome, string? Message);
