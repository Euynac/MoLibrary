using System.Globalization;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;
using Monica.JobScheduler.UI.UIJobScheduler.Support;
using Monica.Modules;
using Monica.Testing.Localization;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Support;

public sealed class SchedulerTimePresentationTests
{
    [Fact]
    public void Timestamp_ShouldUseConfiguredSchedulerWallTimeWithMillisecondsAndNoOffset()
    {
        var timeZone = CreateFixedTimeZone();
        var presentation = CreatePresentation(timeZone);

        var formatted = presentation.FormatTimestamp(
            new DateTimeOffset(2026, 8, 14, 1, 2, 3, 456, TimeSpan.Zero));

        formatted.Should().Be("2026-08-14 09:02:03.456");
        formatted.Should().NotContain("+08:00");
        presentation.TimeZoneId.Should().Be(timeZone.Id);
    }

    [Fact]
    public void Duration_ShouldUseMillisecondsBelowOneSecondAndSecondsOtherwise()
    {
        var presentation = CreatePresentation(CreateFixedTimeZone());

        presentation.FormatDuration(TimeSpan.FromMilliseconds(124.6)).Should().Be(
            $"Common:Duration:Milliseconds [{125D.ToString("F0", CultureInfo.CurrentCulture)}]");
        presentation.FormatDuration(TimeSpan.FromSeconds(1.234)).Should().Be(
            $"Common:Duration:Seconds [{1.234D.ToString("F2", CultureInfo.CurrentCulture)}]");
        presentation.FormatDuration(TimeSpan.FromSeconds(90)).Should().Be(
            $"Common:Duration:Seconds [{90D.ToString("F2", CultureInfo.CurrentCulture)}]");
        presentation.FormatDuration(TimeSpan.FromHours(2.5)).Should().Be(
            $"Common:Duration:Seconds [{TimeSpan.FromHours(2.5).TotalSeconds.ToString("F2", CultureInfo.CurrentCulture)}]");
        presentation.FormatDuration(TimeSpan.FromDays(2)).Should().Be(
            $"Common:Duration:Seconds [{TimeSpan.FromDays(2).TotalSeconds.ToString("F2", CultureInfo.CurrentCulture)}]");
        presentation.FormatDuration(null).Should().Be("—");
    }

    [Fact]
    public void SchedulerWallTime_ShouldRoundTripThroughConfiguredTimezone()
    {
        var presentation = CreatePresentation(CreateFixedTimeZone());
        var wallTime = new DateTime(2026, 8, 14, 9, 30, 15, 250, DateTimeKind.Unspecified);

        var utc = presentation.ConvertSchedulerWallTimeToUtc(wallTime);

        utc.Should().Be(new DateTimeOffset(2026, 8, 14, 1, 30, 15, 250, TimeSpan.Zero));
        presentation.ToSchedulerWallTime(utc).Should().Be(wallTime);
    }

    [Fact]
    public void GetStartOfSchedulerDayUtc_WhenTimezoneIsFixedUtcPlusEight_ShouldReturnSchedulerMidnight()
    {
        var presentation = CreatePresentation(CreateFixedTimeZone());

        var start = presentation.GetStartOfSchedulerDayUtc(
            new DateTimeOffset(2026, 8, 14, 4, 15, 0, TimeSpan.Zero));

        start.Should().Be(new DateTimeOffset(2026, 8, 13, 16, 0, 0, TimeSpan.Zero));
        presentation.ToSchedulerWallTime(start).Should().Be(
            new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void GetStartOfSchedulerDayUtc_WhenSchedulerDayStartsBeforeDaylightSavingTransition_ShouldUseMidnightOffset()
    {
        var presentation = CreatePresentation(CreateDaylightSavingTimeZone());

        var start = presentation.GetStartOfSchedulerDayUtc(
            new DateTimeOffset(2026, 3, 8, 16, 0, 0, TimeSpan.Zero));

        start.Should().Be(new DateTimeOffset(2026, 3, 8, 5, 0, 0, TimeSpan.Zero));
        presentation.ToSchedulerWallTime(start).Should().Be(
            new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void GetStartOfSchedulerDayUtc_WhenMidnightDoesNotExist_ShouldAdvanceToFirstValidWallTime()
    {
        var presentation = CreatePresentation(CreateMidnightTransitionTimeZone());

        var start = presentation.GetStartOfSchedulerDayUtc(
            new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

        start.Should().Be(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        presentation.ToSchedulerWallTime(start).Should().Be(
            new DateTime(2026, 3, 1, 1, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void GetStartOfSchedulerDayUtc_WhenMidnightRepeats_ShouldSelectEarliestRepresentedInstant()
    {
        var presentation = CreatePresentation(CreateMidnightTransitionTimeZone());

        var start = presentation.GetStartOfSchedulerDayUtc(
            new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));

        start.Should().Be(new DateTimeOffset(2026, 10, 31, 23, 0, 0, TimeSpan.Zero));
        presentation.ToSchedulerWallTime(start).Should().Be(
            new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void InvalidDaylightSavingWallTime_ShouldFailValidation()
    {
        var presentation = CreatePresentation(CreateDaylightSavingTimeZone());

        var converted = presentation.TryConvertSchedulerWallTimeToUtc(
            new DateTime(2026, 3, 8, 2, 30, 0, DateTimeKind.Unspecified),
            out _);

        converted.Should().BeFalse();
    }

    [Fact]
    public void JobDisplayName_ShouldPreferDurableSnapshotAndFallbackOnlyWhenEmpty()
    {
        var template = new JobExecutionTemplate
        {
            SchedulerScopeKey = "scope-1",

            OwnerKey = "owner-1",

            JobKey = "Sample.Jobs.GenerateReport",
            JobName = "Generate report"
        };

        JobSchedulerUiPresentation.GetJobDisplayName(template).Should().Be("Generate report");
        JobSchedulerUiPresentation.GetJobDisplayName(template with { JobName = string.Empty })
            .Should().Be("GenerateReport");
    }

    private static SchedulerTimePresentation CreatePresentation(TimeZoneInfo timeZone) => new(
        Options.Create(new ModuleJobSchedulerOption { CronTimeZone = timeZone }),
        new EchoStringLocalizer<JobSchedulerResource>());

    internal static TimeZoneInfo CreateFixedTimeZone() => TimeZoneInfo.CreateCustomTimeZone(
        "Scheduler/Test+08",
        TimeSpan.FromHours(8),
        "Scheduler test time",
        "Scheduler test time");

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
            "Scheduler/Test-DST",
            TimeSpan.FromHours(-5),
            "Scheduler daylight test time",
            "Scheduler standard test time",
            "Scheduler daylight test time",
            [adjustment]);
    }

    private static TimeZoneInfo CreateMidnightTransitionTimeZone()
    {
        var daylightStart = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            new DateTime(1, 1, 1, 0, 0, 0),
            3,
            1);
        var daylightEnd = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            new DateTime(1, 1, 1, 1, 0, 0),
            11,
            1);
        var adjustment = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31),
            TimeSpan.FromHours(1),
            daylightStart,
            daylightEnd);
        return TimeZoneInfo.CreateCustomTimeZone(
            "Scheduler/Test-Midnight-DST",
            TimeSpan.Zero,
            "Scheduler midnight-transition test time",
            "Scheduler midnight-transition standard time",
            "Scheduler midnight-transition daylight time",
            [adjustment]);
    }
}
