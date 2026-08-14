using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.UI.Components;
using Monica.JobScheduler.UI.Pages;
using Monica.JobScheduler.UI.UIJobScheduler.Components;
using Monica.JobScheduler.UI.UIJobScheduler.Executions.State;
using MudBlazor;
using Test.Monica.JobScheduler.UI.Infrastructure;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Pages;

public sealed class SchedulerTimeAndTitlePresentationTests
{
    private static readonly DateTimeOffset NOW =
        new(2026, 8, 14, 1, 2, 3, 456, TimeSpan.Zero);

    [Fact]
    public async Task OverviewRecentActivity_ShouldUseSchedulerTimeAndDurableJobName()
    {
        var clock = new ManualTimeProvider(NOW);
        var timeZone = CreateFixedTimeZone();
        await using var context = new JobSchedulerUiTestContext(
            schedulerTimeZone: timeZone,
            timeProvider: clock);
        var execution = await EnqueueAsync(context, "overview-time-title-0001");

        var cut = context.Render<SchedulerOverviewPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".scheduler-workspace__timezone").TextContent.Should()
                .Contain($"Workspace:TimeZone [{timeZone.Id}]");
            var activity = cut.Find(".recent-panel__event-link");
            activity.TextContent.Should().Contain(context.TriggeredDefinition.Declaration.JobName);
            activity.TextContent.Should().Contain(context.TriggeredDefinition.Declaration.JobKey);
            activity.TextContent.Should().Contain("2026-08-14 09:02:03.456");
            activity.GetAttribute("href").Should().Contain(execution.InstanceId);
        });
    }

    [Fact]
    public async Task ExecutionLedger_ShouldKeepDetailsExplicitAndRenderDurableJobName()
    {
        var clock = new ManualTimeProvider(NOW);
        await using var context = new JobSchedulerUiTestContext(
            schedulerTimeZone: CreateFixedTimeZone(),
            timeProvider: clock);
        var execution = await EnqueueAsync(context, "ledger-time-title-0001");

        var cut = context.Render<JobExecutionsPage>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain(execution.InstanceId));
        var identity = cut.Find(".execution-table__identity");
        identity.LocalName.Should().Be("div");
        identity.QuerySelector("button").Should().BeNull();
        identity.TextContent.Should().Contain(context.TriggeredDefinition.Declaration.JobName);
        identity.TextContent.Should().Contain(context.TriggeredDefinition.Declaration.JobKey);
        cut.Markup.Should().Contain("2026-08-14 09:02:03.456");
        cut.Find(".execution-table__actions button[aria-label='Executions:Actions:Details']")
            .Should().NotBeNull();
    }

    [Fact]
    public async Task ExecutionDetail_ShouldLeadWithDurableJobNameAndKeepJobKeyAsEvidence()
    {
        var clock = new ManualTimeProvider(NOW);
        await using var context = new JobSchedulerUiTestContext(
            schedulerTimeZone: CreateFixedTimeZone(),
            timeProvider: clock);
        var execution = await EnqueueAsync(context, "detail-time-title-0001");
        var dialogService = context.Services.GetRequiredService<IDialogService>();

        await dialogService.ShowAsync<ExecutionDetailDialog>(
            "Execution",
            new DialogParameters<ExecutionDetailDialog>
            {
                { dialog => dialog.InstanceId, execution.InstanceId }
            });

        context.DialogProvider.WaitForAssertion(() =>
        {
            var title = context.DialogProvider.Find(".execution-detail__title");
            title.TextContent.Should().Contain(context.TriggeredDefinition.Declaration.JobName);
            title.TextContent.Should().Contain(context.TriggeredDefinition.Declaration.JobKey);
            context.DialogProvider.Markup.Should().Contain("2026-08-14 09:02:03.456");
        });
    }

    [Fact]
    public async Task CustomExecutionRange_ShouldInterpretCalendarBoundariesInSchedulerTimezone()
    {
        var clock = new ManualTimeProvider(NOW);
        await using var context = new JobSchedulerUiTestContext(
            schedulerTimeZone: CreateFixedTimeZone(),
            timeProvider: clock);
        clock.SetUtcNow(new DateTimeOffset(2026, 8, 13, 15, 59, 59, TimeSpan.Zero));
        await EnqueueAsync(context, "outside-before-scheduler-day");
        clock.SetUtcNow(new DateTimeOffset(2026, 8, 13, 16, 0, 0, TimeSpan.Zero));
        var included = await EnqueueAsync(context, "inside-scheduler-day");
        clock.SetUtcNow(new DateTimeOffset(2026, 8, 14, 16, 0, 0, TimeSpan.Zero));
        await EnqueueAsync(context, "outside-after-scheduler-day");
        await using var state = context.Services.GetRequiredService<JobExecutionsStateFactory>().CreatePageState();
        state.TimeRange = ExecutionTimeRange.Custom;
        state.CustomStartDate = new DateTime(2026, 8, 14);
        state.CustomEndDate = new DateTime(2026, 8, 14);

        await state.InitializeAsync();
        await state.LoadTableAsync(
            new TableState
            {
                PageSize = 20,
                SortLabel = nameof(JobExecutionSortField.CreatedAtUtc),
                SortDirection = SortDirection.Descending
            },
            Xunit.TestContext.Current.CancellationToken);

        state.Executions.Should().ContainSingle()
            .Which.InstanceId.Should().Be(included.InstanceId);
    }

    [Fact]
    public async Task CronInspector_ShouldKeepDraftScheduleTimezoneAndOmitVisibleOffsets()
    {
        var clock = new ManualTimeProvider(NOW);
        await using var context = new JobSchedulerUiTestContext(
            schedulerTimeZone: CreateFixedTimeZone(),
            timeProvider: clock);
        var summary = await context.Store.GetOperationalSummaryAsync(
            JobSchedulerUiTestContext.SCOPE,
            context.RecurringDefinition.Declaration.JobKey,
            Xunit.TestContext.Current.CancellationToken);
        var dialogService = context.Services.GetRequiredService<IDialogService>();

        await dialogService.ShowAsync<CronScheduleInspectorDialog>(
            "Cron",
            new DialogParameters<CronScheduleInspectorDialog>
            {
                { dialog => dialog.Definition, context.RecurringDefinition },
                { dialog => dialog.OperationalSummary, summary },
                { dialog => dialog.ObservedAtUtc, NOW }
            });

        context.DialogProvider.WaitForAssertion(() =>
        {
            context.DialogProvider.Markup.Should().Contain("CronInspector:Preview:SchedulerTime");
            var previewTimes = context.DialogProvider
                .FindAll(".cron-inspector__preview-scroll tbody code")
                .Select(element => element.TextContent.Trim())
                .ToArray();
            previewTimes.Should().Contain("2026-08-14 01:05:00.000");
            previewTimes.Should().Contain("2026-08-14 09:05:00.000");
            previewTimes.Should().OnlyContain(value =>
                !value.Contains("+00:00", StringComparison.Ordinal)
                && !value.Contains("+08:00", StringComparison.Ordinal));
        });
    }

    private static Task<JobExecutionInstance> EnqueueAsync(
        JobSchedulerUiTestContext context,
        string instanceId) => context.Store.EnqueueAsync(
        new JobEnqueueRequest
        {
            SchedulerScopeKey = JobSchedulerUiTestContext.SCOPE,
            InstanceId = instanceId,
            JobKey = context.TriggeredDefinition.Declaration.JobKey,
            ExpectedOwnerId = context.TriggeredDefinition.OwnerId,
            ExpectedJobRevisionId = context.TriggeredDefinition.JobRevisionId,
            JobArgs = "{}"
        },
        Xunit.TestContext.Current.CancellationToken);

    private static TimeZoneInfo CreateFixedTimeZone() => TimeZoneInfo.CreateCustomTimeZone(
        "Scheduler/Test+08",
        TimeSpan.FromHours(8),
        "Scheduler test time",
        "Scheduler test time");

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        internal void SetUtcNow(DateTimeOffset value)
        {
            _utcNow = value;
        }
    }
}
