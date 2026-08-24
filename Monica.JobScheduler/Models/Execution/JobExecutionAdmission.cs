using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Models.Execution;

/// <summary>
/// Centralizes origin-sensitive execution admission invariants shared by durable store providers.
/// </summary>
internal static class JobExecutionAdmission
{
    private const string RECURRING_RUN_NOW_REASON = "Operator requested an immediate recurring execution";

    internal static JobEnqueueRequest CreateRecurringRunNowRequest(JobRecurringRunNowCommand command) => new()
    {
        InstanceId = command.InstanceId,
        SchedulerScopeKey = command.SchedulerScopeKey,
        JobKey = command.JobKey,
        ExpectedOwnerId = command.ExpectedOwnerId,
        ExpectedJobRevisionId = command.ExpectedJobRevisionId,
        EnqueueReason = RECURRING_RUN_NOW_REASON
    };

    internal static void Validate(
        JobExecutionTemplate template,
        JobExecutionOrigin origin,
        DateTimeOffset? recurringOccurrenceUtc,
        JobExecutionSkipReason? skipReason)
    {
        var isScheduledOccurrence = origin == JobExecutionOrigin.RecurringSchedule;
        if (isScheduledOccurrence != recurringOccurrenceUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Only schedule-originated executions must identify a recurring occurrence.");
        }

        var expectedJobType = origin == JobExecutionOrigin.Triggered
            ? JobType.Triggered
            : JobType.Recurring;
        if (template.JobType != expectedJobType)
        {
            throw new InvalidOperationException("The execution origin does not match the captured job type.");
        }

        if (skipReason.HasValue && !isScheduledOccurrence)
        {
            throw new InvalidOperationException("Only a scheduled recurring occurrence can be skipped during admission.");
        }
    }
}
