using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models.Analytics;
using Monica.JobScheduler.Providers;
using Monica.JobScheduler.Services.Support;
using Monica.Modules;
using Monica.Testing.Results;
using Xunit;

namespace Test.Monica.JobScheduler.Facades;

public sealed class JobSchedulerFacadeAnalyticsTests
{
    [Fact]
    public async Task GetExecutionAnalyticsAsync_ShouldUseTheConfiguredScopeAndPreserveBounds()
    {
        var now = new DateTimeOffset(2026, 8, 13, 4, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var facade = new JobSchedulerFacade(
            new InMemoryJobSchedulerStore(timeProvider),
            Options.Create(new ModuleJobSchedulerOption { SchedulerScopeKey = "facade-analytics" }),
            timeProvider,
            new JobSchedulerRuntimeState(),
            NullLogger<JobSchedulerFacade>.Instance);
        var query = new JobExecutionAnalyticsQuery
        {
            StartTimeUtc = now.AddHours(-24),
            EndTimeUtc = now,
            BucketSize = JobExecutionAnalyticsBucketSize.Hour
        };

        var snapshot = (await facade.GetExecutionAnalyticsAsync(
            query,
            TestContext.Current.CancellationToken)).ShouldSucceed()!;

        snapshot.StartTimeUtc.Should().Be(query.StartTimeUtc);
        snapshot.EndTimeUtc.Should().Be(query.EndTimeUtc);
        snapshot.Trend.Should().HaveCount(24);
        snapshot.StateTotals.Values.Should().AllSatisfy(static count => count.Should().Be(0));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
