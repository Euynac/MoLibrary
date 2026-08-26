using AwesomeAssertions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Analytics;
using Monica.JobScheduler.Models.Execution;
using Test.Monica.JobScheduler.Stores.Shared;
using Xunit;

namespace Test.Monica.JobScheduler.Stores;

public sealed class JobSchedulerAnalyticsStoreTests
{
    private static readonly DateTimeOffset RANGE_START = new(2026, 8, 2, 0, 0, 0, TimeSpan.Zero);
    private const string WORKER = "analytics-worker";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Analytics_ShouldSeparateCreatedCohortFromCompletionWindowAndCalculateDurationDistribution(
        bool useEfCore)
    {
        await using var fixture = await StoreFixture.CreateAsync(useEfCore, RANGE_START.AddMinutes(-5));
        await fixture.SyncAsync(
            StoreFixture.OWNER_A,
            StoreFixture.TriggeredDeclaration("jobs.alpha"),
            StoreFixture.TriggeredDeclaration("jobs.beta"));

        await RunAndCompleteAsync(fixture, StoreFixture.OWNER_A, "jobs.alpha", "before-range",
            JobAttemptOutcome.Succeeded, TimeSpan.FromMinutes(10));
        // Both never-completed executions enter the durable ledger inside the analytics cohort window.
        await fixture.EnqueueTriggeredAsync(
            StoreFixture.OWNER_A,
            "jobs.alpha",
            jobArgs: "{\"v\":\"still-queued\"}",
            instanceId: "still-queued",
            availableAtUtc: RANGE_START.AddHours(2));
        await fixture.EnqueueTriggeredAsync(
            StoreFixture.OWNER_A,
            "jobs.beta",
            jobArgs: "{\"v\":\"cancelled-beta\"}",
            instanceId: "cancelled-beta",
            availableAtUtc: RANGE_START.AddMinutes(30));
        await RunAndCompleteAsync(fixture, StoreFixture.OWNER_A, "jobs.alpha", "failed-alpha",
            JobAttemptOutcome.Failed, TimeSpan.FromMinutes(20));
        await RunAndCompleteAsync(fixture, StoreFixture.OWNER_A, "jobs.beta", "succeeded-beta",
            JobAttemptOutcome.Succeeded, TimeSpan.FromMinutes(25));
        var cancelled = (await fixture.ClaimAsync(StoreFixture.OWNER_A, WORKER, ["jobs.beta"]))
            .Single(lease => lease.Execution.InstanceId == "cancelled-beta");
        await fixture.Store.RequestCancellationAsync(fixture.Scope, "cancelled-beta", cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.CompleteAttemptAsync(new JobAttemptCompletion
        {
            LeaseKey = cancelled.LeaseKey,
            Outcome = JobAttemptOutcome.Cancelled
        }, TestContext.Current.CancellationToken);

        var snapshot = await fixture.Store.GetExecutionAnalyticsAsync(fixture.Scope, new JobExecutionAnalyticsQuery
        {
            StartTimeUtc = RANGE_START,
            EndTimeUtc = RANGE_START.AddHours(1)
        }, TestContext.Current.CancellationToken);

        snapshot.StateTotals[JobExecutionState.Queued].Should().Be(1);
        snapshot.CompletedTerminalCount.Should().Be(4);
        snapshot.ExecutedTerminalCount.Should().Be(4);
        snapshot.Reliability.Should().BeApproximately(2.0 / 3, 0.0001);
        snapshot.Duration.Count.Should().Be(4);
        snapshot.Duration.Minimum.Should().Be(TimeSpan.Zero);
        snapshot.Duration.Maximum.Should().Be(TimeSpan.FromMinutes(25));
        snapshot.Trend.Sum(bucket => bucket.SucceededCount).Should().Be(2);
        snapshot.Trend.Sum(bucket => bucket.FailedCount).Should().Be(1);
        snapshot.Trend.Sum(bucket => bucket.CancelledCount).Should().Be(1);
        snapshot.TopJobsByVolume.Should().NotBeEmpty();
        snapshot.TopJobsByVolume.First().JobKey.Should().Be("jobs.alpha");
        snapshot.SlowestExecutions
            .Select(execution => execution.CompletedAtUtc - execution.StartedAtUtc)
            .Should().BeInDescendingOrder(duration => duration);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Analytics_ShouldHonorOwnerAndJobFilters(bool useEfCore)
    {
        await using var fixture = await StoreFixture.CreateAsync(useEfCore, RANGE_START);
        await fixture.SyncAsync(
            StoreFixture.OWNER_A,
            StoreFixture.TriggeredDeclaration("jobs.alpha"),
            StoreFixture.TriggeredDeclaration("jobs.beta"));
        await fixture.SyncAsync(StoreFixture.OWNER_B, StoreFixture.TriggeredDeclaration("jobs.alpha"));

        await RunAndCompleteAsync(fixture, StoreFixture.OWNER_A, "jobs.alpha", "a-1",
            JobAttemptOutcome.Succeeded, TimeSpan.FromMinutes(5));
        await RunAndCompleteAsync(fixture, StoreFixture.OWNER_A, "jobs.beta", "a-2",
            JobAttemptOutcome.Failed, TimeSpan.FromMinutes(5));
        // The same JobKey under another owner must stay separated by the owner filter.
        await RunAndCompleteAsync(fixture, StoreFixture.OWNER_B, "jobs.alpha", "b-1",
            JobAttemptOutcome.Succeeded, TimeSpan.FromMinutes(5));

        var ownerScoped = await fixture.Store.GetExecutionAnalyticsAsync(fixture.Scope, new JobExecutionAnalyticsQuery
        {
            StartTimeUtc = RANGE_START,
            EndTimeUtc = RANGE_START.AddHours(1),
            OwnerKey = StoreFixture.OWNER_A
        }, TestContext.Current.CancellationToken);
        ownerScoped.CompletedTerminalCount.Should().Be(2);

        var ownerJobScoped = await fixture.Store.GetExecutionAnalyticsAsync(fixture.Scope,
            new JobExecutionAnalyticsQuery
            {
                StartTimeUtc = RANGE_START,
                EndTimeUtc = RANGE_START.AddHours(1),
                OwnerKey = StoreFixture.OWNER_B,
                JobKey = "jobs.alpha"
            }, TestContext.Current.CancellationToken);
        ownerJobScoped.CompletedTerminalCount.Should().Be(1);
    }

    [Fact]
    public async Task Analytics_InvalidBoundsShouldBeRejected()
    {
        await using var fixture = await StoreFixture.CreateAsync(false, RANGE_START);
        Func<Task> act = () => fixture.Store.GetExecutionAnalyticsAsync(fixture.Scope, new JobExecutionAnalyticsQuery
        {
            StartTimeUtc = RANGE_START,
            EndTimeUtc = RANGE_START
        });
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private static async Task RunAndCompleteAsync(
        StoreFixture fixture,
        string ownerKey,
        string jobKey,
        string instanceId,
        JobAttemptOutcome outcome,
        TimeSpan duration)
    {
        await fixture.EnqueueTriggeredAsync(
            ownerKey,
            jobKey,
            jobArgs: "{\"v\":\"payload\"}",
            instanceId: instanceId);
        var lease = (await fixture.ClaimAsync(
                ownerKey,
                WORKER,
                [jobKey],
                leaseDuration: TimeSpan.FromHours(2)))
            .Single(item => item.Execution.InstanceId == instanceId);
        fixture.Time.Advance(duration);
        await fixture.Store.CompleteAttemptAsync(new JobAttemptCompletion
        {
            LeaseKey = lease.LeaseKey,
            Outcome = outcome
        }, TestContext.Current.CancellationToken);
    }
}
