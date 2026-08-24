using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Execution;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Services;

/// <summary>
/// Executes one scheduler-owned job attempt through the shared execution pipeline.
/// The durable store and worker remain responsible for state, timeout, concurrency, retry, and fencing.
/// </summary>
public sealed class JobExecutor
{
    private static readonly ConcurrentDictionary<Type, Func<JobExecutor, object, JobExecutionContext, Task>>
        TRIGGERED_EXECUTORS = new();

    public async Task ExecuteRecurringJobAsync(JobExecutionContext context)
    {
        var job = ResolveJob(context);
        if (job is not IRecurringJob recurringJob)
        {
            throw new InvalidOperationException(
                $"Job type does not implement {nameof(IRecurringJob)}: {context.JobType.FullName}");
        }

        var descriptor = ExecutionDescriptor.ForInterface<ExecutionUnit, ExecutionUnit>(
            JobSchedulerExecutionPoints.RecurringAttempt,
            context.JobType,
            typeof(IRecurringJob),
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.None);
        await ExecuteAttemptAsync(
            job,
            context,
            descriptor,
            ExecutionUnit.Value,
            JobType.Recurring,
            () => recurringJob.ExecuteAsync(context.CancellationToken)).ConfigureAwait(false);
    }

    public Task ExecuteTriggeredJobAsync(JobExecutionContext context)
    {
        var job = ResolveJob(context);
        var executor = TRIGGERED_EXECUTORS.GetOrAdd(context.JobType, CreateTriggeredExecutor);
        return executor(this, job, context);
    }

    private async Task ExecuteTriggeredJobAsync<TArgs>(
        object job,
        JobExecutionContext context)
        where TArgs : class
    {
        if (job is not ITriggeredJob<TArgs> triggeredJob)
        {
            throw new InvalidOperationException(
                $"Job type does not implement ITriggeredJob<{typeof(TArgs).FullName}>: {context.JobType.FullName}");
        }

        if (context.JobArgs is not TArgs arguments)
        {
            throw new InvalidOperationException(
                $"Triggered job '{context.JobType.FullName}' requires arguments of type '{typeof(TArgs).FullName}'.");
        }

        var descriptor = ExecutionDescriptor.ForInterface<TArgs, ExecutionUnit>(
            JobSchedulerExecutionPoints.TriggeredAttempt,
            context.JobType,
            typeof(ITriggeredJob<TArgs>),
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.None);
        await ExecuteAttemptAsync(
            job,
            context,
            descriptor,
            arguments,
            JobType.Triggered,
            () => triggeredJob.ExecuteAsync(arguments, context.CancellationToken)).ConfigureAwait(false);
    }

    private static object ResolveJob(JobExecutionContext context)
    {
        return context.ServiceProvider.GetService(context.JobType)
               ?? throw new InvalidOperationException(
                   $"The job type is not registered in DI: {context.JobType.FullName}");
    }

    private static Func<JobExecutor, object, JobExecutionContext, Task> CreateTriggeredExecutor(Type jobType)
    {
        var contract = jobType.GetInterfaces().SingleOrDefault(
            static candidate => candidate.IsGenericType
                                && candidate.GetGenericTypeDefinition() == typeof(ITriggeredJob<>))
            ?? throw new InvalidOperationException(
                $"Job type does not implement {typeof(ITriggeredJob<>).Name}: {jobType.FullName}");
        var argumentsType = contract.GetGenericArguments()[0];

        var executorParameter = Expression.Parameter(typeof(JobExecutor), "executor");
        var jobParameter = Expression.Parameter(typeof(object), "job");
        var contextParameter = Expression.Parameter(typeof(JobExecutionContext), "context");
        var method = typeof(JobExecutor)
            .GetMethod(
                nameof(ExecuteTriggeredJobAsync),
                BindingFlags.Instance | BindingFlags.NonPublic,
                [typeof(object), typeof(JobExecutionContext)])!
            .MakeGenericMethod(argumentsType);
        var call = Expression.Call(executorParameter, method, jobParameter, contextParameter);

        return Expression.Lambda<Func<JobExecutor, object, JobExecutionContext, Task>>(
            call,
            executorParameter,
            jobParameter,
            contextParameter).Compile();
    }

    private async Task ExecuteAttemptAsync<TInput>(
        object job,
        JobExecutionContext jobContext,
        ExecutionDescriptor descriptor,
        TInput input,
        JobType jobType,
        Func<Task> executeJob)
    {
        var features = new ExecutionFeatureCollection();
        features.Set(new JobExecutionFeature(jobContext.InstanceId, jobType));
        BindExecutionLogWriter(job, jobContext);
        try
        {
            await jobContext.ServiceProvider.GetRequiredService<IExecutionPipeline>()
                .ExecuteAsync(
                    descriptor,
                    input,
                    job,
                    executeJob,
                    jobContext.CancellationToken,
                    features)
                .ConfigureAwait(false);
        }
        finally
        {
            ClearExecutionLogWriter(job);
        }
    }

    private static void BindExecutionLogWriter(object job, JobExecutionContext context)
    {
        if (job is not IJobExecutionLogBindingTarget target)
        {
            return;
        }

        target.BindExecutionLogWriter(new JobExecutionLogWriter(
            context.InstanceId,
            context.WriteExecutionLogAsync));
    }

    private static void ClearExecutionLogWriter(object job)
    {
        if (job is IJobExecutionLogBindingTarget target)
        {
            target.ClearExecutionLogWriter();
        }
    }
}
