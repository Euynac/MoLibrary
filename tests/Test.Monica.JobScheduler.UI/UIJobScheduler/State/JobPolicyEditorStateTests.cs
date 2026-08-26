using AwesomeAssertions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Definitions;
using Monica.JobScheduler.Models.Operations;
using Monica.JobScheduler.UI.UIJobScheduler.State;
using Test.Monica.JobScheduler.UI.Infrastructure;
using Xunit;

namespace Test.Monica.JobScheduler.UI.UIJobScheduler.State;

public sealed class JobPolicyEditorStateTests
{
    private static readonly DateTimeOffset OBSERVED_AT =
        new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RebaseAfterConflict_ShouldPreserveCronIntentAndAcceptConcurrentRetentionChange()
    {
        await using var context = new JobSchedulerUiTestContext();
        var initial = await GetRecurringSummaryAsync(context);
        var state = JobPolicyEditorState.Create(initial.Definition, initial, OBSERVED_AT);
        state.Schedule!.SetExpression("0 */15 * * * *");

        var update = await context.Store.UpdatePolicyAsync(
            JobSchedulerUiTestContext.SCOPE,
            initial.Definition.OwnerKey,
            initial.Definition.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = initial.Definition.Policy.Overrides with { MaxRetentionDays = 30 },
                ExpectedConcurrencyStamp = initial.Definition.Policy.ConcurrencyStamp
            },
            Xunit.TestContext.Current.CancellationToken);
        update.Overrides.MaxRetentionDays.Should().Be(30);
        var latest = await GetRecurringSummaryAsync(context);

        state.RebaseAfterConflict(latest.Definition, latest, OBSERVED_AT);

        var rebased = state.CreateChange().Overrides;
        rebased.ScheduleOverride!.CronExpression.Should().Be("0 */15 * * * *");
        rebased.MaxRetentionDays.Should().Be(30);
        state.HasSameFieldConflict.Should().BeFalse();
        state.ExpectedConcurrencyStamp.Should().Be(latest.Definition.Policy.ConcurrencyStamp);
    }

    [Fact]
    public async Task CreateChange_WhenTimeoutExceedsSupportedRange_ShouldRejectDraftWithoutOverflow()
    {
        await using var context = new JobSchedulerUiTestContext();
        var initial = await GetRecurringSummaryAsync(context);
        var state = JobPolicyEditorState.Create(initial.Definition, initial, OBSERVED_AT);
        state.EnableTimeoutOverride();
        state.TimeoutHours = int.MaxValue;

        state.IsTimeoutValid.Should().BeFalse();
        state.EffectiveTimeout.Should().Be(TimeSpan.Zero);
        Action act = () => state.CreateChange();
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(30, 0, 0, 30)]
    [InlineData(5445, 1, 30, 45)]
    public async Task EnableTimeoutOverride_ShouldPreserveWholeSecondPrecision(
        int totalSeconds,
        int expectedHours,
        int expectedMinutes,
        int expectedSeconds)
    {
        await using var context = new JobSchedulerUiTestContext();
        var initial = await GetRecurringSummaryAsync(context);
        var declaredTimeout = TimeSpan.FromSeconds(totalSeconds);
        var definition = initial.Definition with
        {
            Declaration = initial.Definition.Declaration with { MaxExecutionTimeout = declaredTimeout }
        };
        var state = JobPolicyEditorState.Create(definition, initial, OBSERVED_AT);

        state.EnableTimeoutOverride();

        state.TimeoutHours.Should().Be(expectedHours);
        state.TimeoutMinutes.Should().Be(expectedMinutes);
        state.TimeoutSeconds.Should().Be(expectedSeconds);
        state.IsTimeoutValid.Should().BeTrue();
        state.CreateChange().Overrides.MaxExecutionTimeoutOverride.Should().Be(declaredTimeout);
    }

    [Fact]
    public async Task EnableTimeoutOverride_WhenTimeoutHasFractionalTicks_ShouldPreserveThemExplicitly()
    {
        await using var context = new JobSchedulerUiTestContext();
        var initial = await GetRecurringSummaryAsync(context);
        var declaredTimeout = TimeSpan.FromSeconds(30).Add(TimeSpan.FromTicks(1234));
        var definition = initial.Definition with
        {
            Declaration = initial.Definition.Declaration with { MaxExecutionTimeout = declaredTimeout }
        };
        var state = JobPolicyEditorState.Create(definition, initial, OBSERVED_AT);

        state.EnableTimeoutOverride();

        state.HasSubsecondTimeoutPrecision.Should().BeTrue();
        state.CreateChange().Overrides.MaxExecutionTimeoutOverride.Should().Be(declaredTimeout);
    }

    [Fact]
    public async Task RebaseAfterConflict_WhenRecurringJobBecomesTriggered_ShouldRequireExplicitScheduleDiscard()
    {
        await using var context = new JobSchedulerUiTestContext();
        var initial = await GetRecurringSummaryAsync(context);
        var state = JobPolicyEditorState.Create(initial.Definition, initial, OBSERVED_AT);
        state.Schedule!.SetExpression("0 */15 * * * *");
        state.MaxRetentionDays = 45;
        var latestDefinition = initial.Definition with
        {
            Declaration = initial.Definition.Declaration with
            {
                JobType = JobType.Triggered,
                JobArgsKey = "Sample.Jobs.RecurringCleanupArgs",
                CronExpression = null,
                TimeZoneId = null,
                StartTimeUtc = null,
                EndTimeUtc = null
            },
            Policy = initial.Definition.Policy with { ConcurrencyStamp = "latest-policy-revision" }
        };

        state.RebaseAfterConflict(latestDefinition, null, OBSERVED_AT);

        state.Schedule.Should().BeNull();
        state.HasScheduleTypeConflict.Should().BeTrue();
        state.UnresolvedScheduleDraft!.CronExpression.Should().Be("0 */15 * * * *");
        state.CanSave.Should().BeFalse();
        Action blockedSave = () => state.CreateChange();
        blockedSave.Should().Throw<InvalidOperationException>();

        state.DiscardScheduleDraft();

        state.HasScheduleTypeConflict.Should().BeFalse();
        state.MaxRetentionDays.Should().Be(45);
        state.CanSave.Should().BeTrue();
        var recovered = state.CreateChange();
        recovered.Overrides.ScheduleOverride.Should().Be(latestDefinition.Policy.Overrides.ScheduleOverride);
        recovered.Overrides.MaxRetentionDays.Should().Be(45);
        recovered.ExpectedConcurrencyStamp.Should().Be("latest-policy-revision");
    }

    private static async Task<JobOperationalSummary> GetRecurringSummaryAsync(
        JobSchedulerUiTestContext context) =>
        (await context.Store.GetOperationalSummaryAsync(
            JobSchedulerUiTestContext.SCOPE,
            new JobId(
                JobSchedulerUiTestContext.OWNER,
                context.RecurringDefinition.Declaration.JobKey),
            Xunit.TestContext.Current.CancellationToken))!;
}
