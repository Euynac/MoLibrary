using AwesomeAssertions;
using System.Text.Json;
using Monica.JobScheduler.Models.Definitions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Execution;
using Xunit;

namespace Test.Monica.JobScheduler.Models;

public sealed class JobPolicyModelsTests
{
    [Fact]
    public void Overrides_ResolveShouldProjectEffectiveConfiguration()
    {
        var declaration = StoreFixtureLike.TriggeredDeclaration("jobs.beta");
        var overrides = new JobPolicyOverrides
        {
            DisplayNameOverride = "Beta display",
            MaxConcurrencyOverride = 4,
            RetryCountOverride = 2,
            MaxRetainedHistoryRecords = 50
        };

        var effective = overrides.Resolve(declaration);

        effective.JobName.Should().Be("Beta display");
        effective.MaxConcurrency.Should().Be(4);
        effective.RetryCount.Should().Be(2);
        effective.MaxRetainedHistoryRecords.Should().Be(50);
        effective.MaxExecutionTimeout.Should().Be(declaration.MaxExecutionTimeout);
        effective.Schedule.Should().BeNull();
    }

    [Fact]
    public void Overrides_ScheduleOverrideShouldReplaceCronAndBoundariesForRecurringJobs()
    {
        var declaration = StoreFixtureLike.RecurringDeclaration("jobs.alpha", cron: "0 * * * * *");
        var overrides = new JobPolicyOverrides
        {
            ScheduleOverride = new JobScheduleOverride
            {
                CronExpression = "0 0 * * * *",
                StartTimeUtc = new JobScheduleBoundaryOverride { Value = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                EndTimeUtc = new JobScheduleBoundaryOverride { Value = null }
            }
        };

        var schedule = overrides.Resolve(declaration).Schedule!;

        schedule.CronExpression.Should().Be("0 0 * * * *");
        schedule.TimeZoneId.Should().Be(declaration.TimeZoneId);
        schedule.StartTimeUtc.Should().Be(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));
        schedule.EndTimeUtc.Should().BeNull();
        schedule.GetNextOccurrence(new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero))
            .Should().Be(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Overrides_ValidateShouldRejectInvalidValues()
    {
        var declaration = StoreFixtureLike.TriggeredDeclaration("jobs.beta");
        var emptyName = new JobPolicyOverrides { DisplayNameOverride = " " };
        var act = () => emptyName.Validate(declaration);
        act.Should().Throw<ArgumentException>();

        var zeroConcurrency = new JobPolicyOverrides { MaxConcurrencyOverride = 0 };
        act = () => zeroConcurrency.Validate(declaration);
        act.Should().Throw<ArgumentOutOfRangeException>();

        var triggeredSchedule = new JobPolicyOverrides
        {
            ScheduleOverride = new JobScheduleOverride { CronExpression = "0 * * * * *" }
        };
        act = () => triggeredSchedule.Validate(declaration);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Overrides_SerializationShouldKeepOnlyAuthoritativeFields()
    {
        var overrides = new JobPolicyOverrides
        {
            DisplayNameOverride = "Beta",
            MaxConcurrencyOverride = 2
        };
        var serialized = JsonSerializer.Serialize(overrides);
        serialized.Should().Contain("DisplayNameOverride").And.Contain("MaxConcurrencyOverride");
        var roundTrip = JsonSerializer.Deserialize<JobPolicyOverrides>(serialized);
        roundTrip.Should().Be(overrides);
    }

    private static class StoreFixtureLike
    {
        internal static JobDeclaration RecurringDeclaration(string jobKey, string cron) => new()
        {
            JobKey = jobKey,
            JobName = jobKey,
            JobType = JobType.Recurring,
            CronExpression = cron,
            TimeZoneId = "UTC"
        };

        internal static JobDeclaration TriggeredDeclaration(string jobKey) => new()
        {
            JobKey = jobKey,
            JobName = jobKey,
            JobType = JobType.Triggered,
            JobArgsKey = $"{jobKey}Args",
            MaxExecutionTimeout = TimeSpan.FromMinutes(5)
        };
    }
}
