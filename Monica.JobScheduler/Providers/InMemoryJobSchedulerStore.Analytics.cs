using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Analytics;
using Monica.JobScheduler.Models.Execution;

namespace Monica.JobScheduler.Providers;

public sealed partial class InMemoryJobSchedulerStore
{
    /// <inheritdoc />
    public Task<JobExecutionAnalyticsSnapshot> GetExecutionAnalyticsAsync(
        string schedulerScopeKey,
        JobExecutionAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        JobSchedulerIdentity.ValidateStandard(schedulerScopeKey, nameof(schedulerScopeKey));
        ArgumentNullException.ThrowIfNull(query);
        var range = query.ValidateAndNormalize();
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var scoped = _executionInstances.Values.Where(execution =>
                string.Equals(
                    execution.Template.SchedulerScopeKey,
                    schedulerScopeKey,
                    StringComparison.Ordinal)
                && (query.OwnerKey is null
                    || string.Equals(execution.Template.OwnerKey, query.OwnerKey, StringComparison.Ordinal))
                && (query.JobKey is null
                    || string.Equals(execution.Template.JobKey, query.JobKey, StringComparison.Ordinal)));
            var cohort = scoped.Where(execution =>
                execution.CreatedAtUtc >= range.StartTimeUtc && execution.CreatedAtUtc < range.EndTimeUtc);
            var cohortGroups = cohort
                .GroupBy(static execution => execution.State)
                .ToDictionary(static group => group.Key, static group => (long)group.Count());
            IReadOnlyDictionary<JobExecutionState, long> stateTotals = Enum.GetValues<JobExecutionState>()
                .ToDictionary(state => state, state => cohortGroups.GetValueOrDefault(state));

            var completions = scoped.Where(execution =>
                    IsTerminal(execution.State)
                    && execution.CompletedAtUtc >= range.StartTimeUtc
                    && execution.CompletedAtUtc < range.EndTimeUtc)
                .ToArray();
            var succeededCount = CountState(completions, JobExecutionState.Succeeded);
            var failedCount = CountState(completions, JobExecutionState.Failed);
            var recurringScheduleDispositions = completions.Where(static execution =>
                    execution.Origin == JobExecutionOrigin.RecurringSchedule
                    && execution.State is (JobExecutionState.Succeeded
                        or JobExecutionState.Failed
                        or JobExecutionState.Skipped))
                .ToArray();
            var recurringScheduleDispositionCount = recurringScheduleDispositions.LongLength;
            var fulfilledRecurringScheduleCount = recurringScheduleDispositions.LongCount(static execution =>
                execution.State is (JobExecutionState.Succeeded or JobExecutionState.Failed));
            var executed = completions.Where(static execution => execution.StartedAtUtc is not null).ToArray();
            var measurableExecutions = executed
                .Where(static execution => execution.CompletedAtUtc >= execution.StartedAtUtc)
                .ToArray();
            var durationTicks = measurableExecutions
                .Select(static execution => (execution.CompletedAtUtc!.Value - execution.StartedAtUtc!.Value).Ticks)
                .Order()
                .ToArray();
            var hours = (range.EndTimeUtc - range.StartTimeUtc).TotalHours;

            return Task.FromResult(new JobExecutionAnalyticsSnapshot
            {
                StartTimeUtc = range.StartTimeUtc,
                EndTimeUtc = range.EndTimeUtc,
                BucketSize = query.BucketSize,
                JobKey = query.JobKey,
                StateTotals = stateTotals,
                CompletedTerminalCount = completions.LongLength,
                ExecutedTerminalCount = executed.LongLength,
                ExecutedThroughputPerHour = executed.LongLength / hours,
                Reliability = JobExecutionAnalyticsMath.Ratio(succeededCount, succeededCount + failedCount),
                RecurringScheduleDispositionCount = recurringScheduleDispositionCount,
                RecurringScheduleFulfillment = JobExecutionAnalyticsMath.Ratio(
                    fulfilledRecurringScheduleCount,
                    recurringScheduleDispositionCount),
                Duration = CreateDurationStatistics(durationTicks),
                Trend = CreateTrend(range, completions),
                TopJobsByVolume = CreateJobRanking(
                    completions,
                    query.TopJobLimit,
                    static item => item.CompletedTerminalCount,
                    requirePositiveMetric: false),
                TopJobsByFailures = CreateJobRanking(
                    completions,
                    query.TopJobLimit,
                    static item => item.FailedCount,
                    requirePositiveMetric: true),
                SlowestExecutions = measurableExecutions
                    .GroupBy(
                        static execution => execution.Template.JobKey,
                        StringComparer.Ordinal)
                    .Select(static group => group
                        .OrderByDescending(static execution =>
                            execution.CompletedAtUtc!.Value - execution.StartedAtUtc!.Value)
                        .ThenBy(static execution => execution.InstanceId, StringComparer.Ordinal)
                        .First())
                    .OrderByDescending(static execution =>
                        execution.CompletedAtUtc!.Value - execution.StartedAtUtc!.Value)
                    .ThenBy(static execution => execution.InstanceId, StringComparer.Ordinal)
                    .Take(query.SlowestExecutionLimit)
                    .Select(static execution => new JobExecutionAnalyticsSlowExecution
                    {
                        InstanceId = execution.InstanceId,
                        JobName = execution.Template.JobName,
                        JobKey = execution.Template.JobKey,
                        State = execution.State,
                        StartedAtUtc = execution.StartedAtUtc!.Value,
                        CompletedAtUtc = execution.CompletedAtUtc!.Value
                    })
                    .ToArray()
            });
        }
    }

    private static long CountState(IEnumerable<StoredExecution> executions, JobExecutionState state) =>
        executions.LongCount(execution => execution.State == state);

    private static JobExecutionDurationStatistics CreateDurationStatistics(long[] orderedDurationTicks)
    {
        if (orderedDurationTicks.Length == 0)
        {
            return new JobExecutionDurationStatistics();
        }

        return new JobExecutionDurationStatistics
        {
            Count = orderedDurationTicks.LongLength,
            Minimum = TimeSpan.FromTicks(orderedDurationTicks[0]),
            Average = TimeSpan.FromTicks(checked((long)orderedDurationTicks.Average(static ticks => (double)ticks))),
            P50 = TimeSpan.FromTicks(NearestRank(orderedDurationTicks, 0.50)),
            P90 = TimeSpan.FromTicks(NearestRank(orderedDurationTicks, 0.90)),
            P95 = TimeSpan.FromTicks(NearestRank(orderedDurationTicks, 0.95)),
            P99 = TimeSpan.FromTicks(NearestRank(orderedDurationTicks, 0.99)),
            Maximum = TimeSpan.FromTicks(orderedDurationTicks[^1])
        };
    }

    private static long NearestRank(IReadOnlyList<long> orderedValues, double percentile)
    {
        var index = Math.Max(0, (int)Math.Ceiling(percentile * orderedValues.Count) - 1);
        return orderedValues[index];
    }

    private static IReadOnlyList<JobExecutionAnalyticsBucket> CreateTrend(
        JobExecutionAnalyticsRange range,
        IReadOnlyCollection<StoredExecution> completions)
    {
        var grouped = completions
            .GroupBy(execution => Math.Min(
                range.BucketCount - 1,
                (int)((execution.CompletedAtUtc!.Value - range.StartTimeUtc).Ticks / range.BucketDuration.Ticks)))
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        return Enumerable.Range(0, range.BucketCount)
            .Select(index =>
            {
                grouped.TryGetValue(index, out var bucket);
                bucket ??= [];
                var start = range.StartTimeUtc.AddTicks(range.BucketDuration.Ticks * index);
                return new JobExecutionAnalyticsBucket
                {
                    StartTimeUtc = start,
                    EndTimeUtc = Min(start.Add(range.BucketDuration), range.EndTimeUtc),
                    SucceededCount = CountState(bucket, JobExecutionState.Succeeded),
                    FailedCount = CountState(bucket, JobExecutionState.Failed),
                    SkippedCount = CountState(bucket, JobExecutionState.Skipped),
                    CancelledCount = CountState(bucket, JobExecutionState.Cancelled),
                    ExecutedTerminalCount = bucket.LongCount(static execution => execution.StartedAtUtc is not null)
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<JobExecutionAnalyticsJobRank> CreateJobRanking(
        IEnumerable<StoredExecution> completions,
        int limit,
        Func<JobExecutionAnalyticsJobRank, long> metric,
        bool requirePositiveMetric)
    {
        var rows = completions
            .GroupBy(static execution => execution.Template.JobKey, StringComparer.Ordinal)
            .Select(group => new JobExecutionAnalyticsJobRank
            {
                JobName = group
                    .OrderByDescending(static execution => execution.CompletedAtUtc)
                    .ThenBy(static execution => execution.InstanceId, StringComparer.Ordinal)
                    .First()
                    .Template.JobName,
                JobKey = group.Key,
                SucceededCount = CountState(group, JobExecutionState.Succeeded),
                FailedCount = CountState(group, JobExecutionState.Failed),
                SkippedCount = CountState(group, JobExecutionState.Skipped),
                CancelledCount = CountState(group, JobExecutionState.Cancelled)
            });
        if (requirePositiveMetric)
        {
            rows = rows.Where(item => metric(item) > 0);
        }

        return rows
            .OrderByDescending(metric)
            .ThenBy(static item => item.JobKey, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
}
