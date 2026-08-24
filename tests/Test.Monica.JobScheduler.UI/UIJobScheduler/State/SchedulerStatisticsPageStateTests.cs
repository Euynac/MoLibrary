using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Models.Analytics;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.UIJobScheduler.State;
using Monica.JobScheduler.UI.UIJobScheduler.Support;
using Monica.Modules;
using Monica.Testing.Localization;
using Xunit;

namespace Test.Monica.JobScheduler.UI.UIJobScheduler.State;

public sealed class SchedulerStatisticsPageStateTests
{
    [Fact]
    public void CreateQuery_WhenTodayUsesFixedUtcPlusEight_ShouldStartAtSchedulerMidnight()
    {
        var now = new DateTimeOffset(2026, 8, 14, 4, 15, 0, TimeSpan.Zero);
        var presentation = CreatePresentation(CreateFixedTimeZone());

        var query = SchedulerStatisticsPageState.CreateQuery(
            SchedulerAnalyticsTimeRange.Today,
            now,
            presentation,
            "Sample.Jobs.DailyReport");

        query.StartTimeUtc.Should().Be(new DateTimeOffset(2026, 8, 13, 16, 0, 0, TimeSpan.Zero));
        query.EndTimeUtc.Should().Be(now);
        query.BucketSize.Should().Be(JobExecutionAnalyticsBucketSize.Hour);
        query.JobKey.Should().Be("Sample.Jobs.DailyReport");
    }

    [Fact]
    public void CreateQuery_WhenTodayContainsDaylightSavingTransition_ShouldStartAtSchedulerMidnight()
    {
        var now = new DateTimeOffset(2026, 3, 8, 16, 0, 0, TimeSpan.Zero);
        var presentation = CreatePresentation(CreateDaylightSavingTimeZone());

        var query = SchedulerStatisticsPageState.CreateQuery(
            SchedulerAnalyticsTimeRange.Today,
            now,
            presentation);

        query.StartTimeUtc.Should().Be(new DateTimeOffset(2026, 3, 8, 5, 0, 0, TimeSpan.Zero));
        query.EndTimeUtc.Should().Be(now);
        query.BucketSize.Should().Be(JobExecutionAnalyticsBucketSize.Hour);
    }

    [Fact]
    public void CreateQuery_WhenTodayStartsExactlyNow_ShouldKeepTheSchedulerDayBoundary()
    {
        var now = new DateTimeOffset(2026, 8, 13, 16, 0, 0, TimeSpan.Zero);

        var query = SchedulerStatisticsPageState.CreateQuery(
            SchedulerAnalyticsTimeRange.Today,
            now,
            CreatePresentation(CreateFixedTimeZone()));

        query.StartTimeUtc.Should().Be(now);
        query.EndTimeUtc.Should().Be(now.AddTicks(1));
        query.BucketSize.Should().Be(JobExecutionAnalyticsBucketSize.Hour);
    }

    [Theory]
    [InlineData(SchedulerAnalyticsTimeRange.Last24Hours, 24, JobExecutionAnalyticsBucketSize.Hour)]
    [InlineData(SchedulerAnalyticsTimeRange.Last7Days, 168, JobExecutionAnalyticsBucketSize.Day)]
    [InlineData(SchedulerAnalyticsTimeRange.Last30Days, 720, JobExecutionAnalyticsBucketSize.Day)]
    public void CreateQuery_WhenRangeIsRolling_ShouldBeTimezoneInvariant(
        SchedulerAnalyticsTimeRange range,
        int hours,
        JobExecutionAnalyticsBucketSize expectedBucketSize)
    {
        var now = new DateTimeOffset(2026, 8, 14, 4, 15, 0, TimeSpan.Zero);

        var fixedZoneQuery = SchedulerStatisticsPageState.CreateQuery(
            range,
            now,
            CreatePresentation(CreateFixedTimeZone()));
        var daylightZoneQuery = SchedulerStatisticsPageState.CreateQuery(
            range,
            now,
            CreatePresentation(CreateDaylightSavingTimeZone()));

        fixedZoneQuery.StartTimeUtc.Should().Be(now.AddHours(-hours));
        fixedZoneQuery.EndTimeUtc.Should().Be(now);
        fixedZoneQuery.BucketSize.Should().Be(expectedBucketSize);
        daylightZoneQuery.StartTimeUtc.Should().Be(fixedZoneQuery.StartTimeUtc);
        daylightZoneQuery.EndTimeUtc.Should().Be(fixedZoneQuery.EndTimeUtc);
        daylightZoneQuery.BucketSize.Should().Be(fixedZoneQuery.BucketSize);
    }

    [Fact]
    public void CreateQuery_WhenBuildingAnalyticsRequest_ShouldRequestTopTenRankingsAndSlowExecutions()
    {
        var query = SchedulerStatisticsPageState.CreateQuery(
            SchedulerAnalyticsTimeRange.Last24Hours,
            new DateTimeOffset(2026, 8, 14, 4, 15, 0, TimeSpan.Zero),
            CreatePresentation(CreateFixedTimeZone()));

        query.TopJobLimit.Should().Be(10);
        query.SlowestExecutionLimit.Should().Be(10);
    }

    private static SchedulerTimePresentation CreatePresentation(TimeZoneInfo timeZone) => new(
        Options.Create(new ModuleJobSchedulerOption { CronTimeZone = timeZone }),
        new EchoStringLocalizer<JobSchedulerResource>());

    private static TimeZoneInfo CreateFixedTimeZone() => TimeZoneInfo.CreateCustomTimeZone(
        "Scheduler/Statistics+08",
        TimeSpan.FromHours(8),
        "Scheduler statistics test time",
        "Scheduler statistics test time");

    private static TimeZoneInfo CreateDaylightSavingTimeZone()
    {
        var daylightStart = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0),
            3,
            2,
            DayOfWeek.Sunday);
        var daylightEnd = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0),
            11,
            1,
            DayOfWeek.Sunday);
        var adjustment = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31),
            TimeSpan.FromHours(1),
            daylightStart,
            daylightEnd);
        return TimeZoneInfo.CreateCustomTimeZone(
            "Scheduler/Statistics-DST",
            TimeSpan.FromHours(-5),
            "Scheduler statistics daylight test time",
            "Scheduler statistics standard test time",
            "Scheduler statistics daylight test time",
            [adjustment]);
    }
}
