using System.Text.Json;
using AwesomeAssertions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Xunit;

namespace Test.Monica.JobScheduler.Models.Catalog;

public sealed class JobPolicyModelsTests
{
    private static readonly DateTimeOffset DECLARED_START = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DECLARED_END = new(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Resolve_WhenEveryPolicyFieldIsOverridden_ShouldProjectEffectiveValuesWithoutChangingTimezone()
    {
        var declaration = CreateDeclaration();
        var overrides = new JobPolicyOverrides
        {
            DisabledOverride = true,
            DisplayNameOverride = " Operator name ",
            DescriptionOverride = new JobDescriptionOverride { Value = null },
            MaxConcurrencyOverride = 4,
            RetryCountOverride = 5,
            MaxExecutionTimeoutOverride = TimeSpan.FromMinutes(30),
            ScheduleOverride = new JobScheduleOverride
            {
                CronExpression = " 0   */5   * * * * ",
                StartTimeUtc = new JobScheduleBoundaryOverride
                {
                    Value = DECLARED_START.AddDays(2).ToOffset(TimeSpan.FromHours(8))
                },
                EndTimeUtc = new JobScheduleBoundaryOverride { Value = null }
            },
            MaxRetainedHistoryRecords = 0,
            MaxRetentionDays = 14
        }.Normalize();

        overrides.Validate(declaration);
        var effective = overrides.Resolve(declaration);

        effective.JobName.Should().Be("Operator name");
        effective.Description.Should().BeNull();
        effective.IsDisabled.Should().BeTrue();
        effective.MaxConcurrency.Should().Be(4);
        effective.RetryCount.Should().Be(5);
        effective.MaxExecutionTimeout.Should().Be(TimeSpan.FromMinutes(30));
        effective.Schedule!.CronExpression.Should().Be("0 */5 * * * *");
        effective.Schedule.TimeZoneId.Should().Be(declaration.TimeZoneId);
        effective.Schedule.StartTimeUtc.Should().Be(DECLARED_START.AddDays(2));
        effective.Schedule.EndTimeUtc.Should().BeNull();
        effective.MaxRetainedHistoryRecords.Should().Be(0);
        effective.MaxRetentionDays.Should().Be(14);
        typeof(JobScheduleOverride).GetProperty("TimeZoneId").Should().BeNull();
    }

    [Fact]
    public void Resolve_WhenEachOverrideIsReset_ShouldRestoreEveryDeclaredOrSchedulerDefault()
    {
        var declaration = CreateDeclaration();
        var allOverrides = new JobPolicyOverrides
        {
            DisabledOverride = true,
            DisplayNameOverride = "Operator name",
            DescriptionOverride = new JobDescriptionOverride { Value = null },
            MaxConcurrencyOverride = 9,
            RetryCountOverride = 8,
            MaxExecutionTimeoutOverride = TimeSpan.FromMinutes(45),
            ScheduleOverride = new JobScheduleOverride
            {
                CronExpression = "0 */5 * * * *",
                StartTimeUtc = new JobScheduleBoundaryOverride { Value = null },
                EndTimeUtc = new JobScheduleBoundaryOverride { Value = null }
            },
            MaxRetainedHistoryRecords = 0,
            MaxRetentionDays = 30
        };
        var reset = allOverrides with
        {
            DisabledOverride = null,
            DisplayNameOverride = null,
            DescriptionOverride = null,
            MaxConcurrencyOverride = null,
            RetryCountOverride = null,
            MaxExecutionTimeoutOverride = null,
            ScheduleOverride = allOverrides.ScheduleOverride! with
            {
                CronExpression = null,
                StartTimeUtc = null,
                EndTimeUtc = null
            },
            MaxRetainedHistoryRecords = null,
            MaxRetentionDays = null
        };

        var normalized = reset.Normalize();
        var effective = normalized.Resolve(declaration);

        normalized.HasAnyOverride.Should().BeFalse();
        normalized.ScheduleOverride.Should().BeNull();
        effective.JobName.Should().Be(declaration.JobName);
        effective.Description.Should().Be(declaration.Description);
        effective.IsDisabled.Should().Be(declaration.IsDisabledByDefault);
        effective.MaxConcurrency.Should().Be(declaration.MaxConcurrency);
        effective.RetryCount.Should().Be(declaration.RetryCount);
        effective.MaxExecutionTimeout.Should().Be(declaration.MaxExecutionTimeout);
        effective.Schedule!.CronExpression.Should().Be(declaration.CronExpression);
        effective.Schedule.TimeZoneId.Should().Be(declaration.TimeZoneId);
        effective.Schedule.StartTimeUtc.Should().Be(DECLARED_START);
        effective.Schedule.EndTimeUtc.Should().Be(DECLARED_END);
        effective.MaxRetainedHistoryRecords.Should().Be(JobPolicy.DEFAULT_MAX_RETAINED_HISTORY_RECORDS);
        effective.MaxRetentionDays.Should().BeNull();
    }

    [Fact]
    public void Serialize_WhenOverridesContainComputedState_ShouldPersistOnlyAuthoritativeFields()
    {
        var overrides = new JobPolicyOverrides
        {
            ScheduleOverride = new JobScheduleOverride { CronExpression = "0 * * * * *" }
        };

        var json = JsonSerializer.Serialize(overrides);

        json.Should().NotContain(nameof(JobPolicyOverrides.HasAnyOverride));
        json.Should().NotContain(nameof(JobScheduleOverride.HasAnyOverride));
    }

    private static JobDeclaration CreateDeclaration() => new()
    {
        JobKey = "jobs.policy-model",
        JobName = "Declared name",
        Description = "Declared description",
        JobType = JobType.Recurring,
        MaxConcurrency = 2,
        RetryCount = 1,
        MaxExecutionTimeout = TimeSpan.FromMinutes(10),
        CronExpression = "0 * * * * *",
        TimeZoneId = TimeZoneInfo.Utc.Id,
        StartTimeUtc = DECLARED_START,
        EndTimeUtc = DECLARED_END
    };
}
