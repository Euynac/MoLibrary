using System.Reflection;
using System.Security.Cryptography;
using BuildingBlocksPlatform.SeedWork;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using MoLibrary.Core.Features;
using MoLibrary.Tool.Web;

namespace BuildingBlocksPlatform.BackgroundWorker.Attributes;

public class SkipWhenPreviousJobIsRunningAttribute : JobFilterAttribute, IClientFilter, IApplyStateFilter
{
    protected static string GetJobId(Type jobArgsType)
    {
        return $"single-job:{WebTool.StringHash(jobArgsType.FullName!, HashAlgorithmName.MD5)}";
    }

    public void OnCreating(CreatingContext context)
    {
        // We can't handle old storages
        if (context.Connection is not JobStorageConnection connection) return;

        // We should run this filter only for background jobs based on 
        // recurring ones
        if (context.Parameters.ContainsKey("RecurringJobId")) return;


        var jobType = context.Job.Args.SingleOrDefault(p => p.GetType().IsClass && p is not string)?.GetType();
        if (jobType?.GetCustomAttribute<SkipWhenPreviousJobIsRunningAttribute>() == null) return;

        var running =
            connection.GetValueFromHash(GetJobId(jobType), "Running");
        if ("yes".Equals(running, StringComparison.OrdinalIgnoreCase))
        {
            context.Canceled = true;
            GlobalLog.LogWarning($"检测到已有相同Job {jobType.FullName}正在执行，该次任务已被忽略");
        }

    }

    public void OnCreated(CreatedContext filterContext)
    {
    }

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        // We can't handle old storages
        if (context.Connection is not JobStorageConnection connection) return;

        var jobType = context.BackgroundJob.Job.Args.SingleOrDefault(p => p.GetType().IsClass && p is not string)?.GetType();
        if (jobType?.GetCustomAttribute<SkipWhenPreviousJobIsRunningAttribute>() == null) return;

        var jobId = GetJobId(jobType);
        if (context.NewState is EnqueuedState)
        {
            ChangeRunningState(context, "yes", jobId);
        }
        else if (context.NewState.IsFinal && !FailedState.StateName.Equals(context.OldStateName, StringComparison.OrdinalIgnoreCase) ||
                 context.NewState is FailedState)
        {
            ChangeRunningState(context, "no", jobId);
        }
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }

    private static void ChangeRunningState(ApplyStateContext context, string state, string jobId)
    {
        if (context.Storage.HasFeature(JobStorageFeatures.Transaction.AcquireDistributedLock))
        {
            // Acquire a lock in newer storages to avoid race conditions
            ((JobStorageTransaction)context.Transaction).AcquireDistributedLock(
                $"lock:{jobId}",
                TimeSpan.FromSeconds(5));
        }

        // Checking whether recurring job exists
        //var job = (context.Connection as JobStorageConnection)!.GetValueFromHash(jobId, "Job");
        //if (string.IsNullOrEmpty(job)) return;

        // Changing the running state
        context.Transaction.SetRangeInHash(jobId,
            [new KeyValuePair<string, string>("Running", state)]);
    }
}