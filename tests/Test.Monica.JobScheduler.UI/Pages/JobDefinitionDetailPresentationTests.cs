using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Analytics;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.UI.Pages;
using Monica.JobScheduler.UI.UIJobScheduler.Components;
using Monica.JobScheduler.UI.UIJobScheduler.State;
using Test.Monica.JobScheduler.UI.Infrastructure;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Pages;

public sealed class JobDefinitionDetailPresentationTests
{
    private static readonly DateTimeOffset OBSERVED_AT =
        new(2026, 8, 14, 2, 3, 4, 567, TimeSpan.Zero);

    [Fact]
    public async Task HealthPanel_ShouldPresentEveryExecutionStateAndBoundedStatistics()
    {
        await using var context = new JobSchedulerUiTestContext();
        var stateTotals = Enum.GetValues<JobExecutionState>()
            .ToDictionary(static state => state, static state => (long)state + 1L);
        var snapshot = new JobExecutionAnalyticsSnapshot
        {
            StartTimeUtc = OBSERVED_AT.AddDays(-30),
            EndTimeUtc = OBSERVED_AT,
            BucketSize = JobExecutionAnalyticsBucketSize.Day,
            StateTotals = stateTotals,
            CompletedTerminalCount = 18,
            ExecutedTerminalCount = 12,
            ExecutedThroughputPerHour = 0.5,
            Reliability = 0.75,
            RecurringScheduleDispositionCount = 10,
            RecurringScheduleFulfillment = 0.9,
            Duration = new JobExecutionDurationStatistics
            {
                Count = 12,
                Minimum = TimeSpan.FromMilliseconds(25),
                Average = TimeSpan.FromMilliseconds(400),
                P50 = TimeSpan.FromMilliseconds(300),
                P95 = TimeSpan.FromSeconds(2),
                P99 = TimeSpan.FromSeconds(3),
                Maximum = TimeSpan.FromSeconds(4)
            },
            Trend =
            [
                new JobExecutionAnalyticsBucket
                {
                    StartTimeUtc = OBSERVED_AT.AddDays(-1),
                    EndTimeUtc = OBSERVED_AT,
                    SucceededCount = 9,
                    FailedCount = 3,
                    SkippedCount = 1
                }
            ],
            TopJobsByVolume = [],
            TopJobsByFailures = [],
            SlowestExecutions = []
        };

        var cut = context.Render<JobHealthPanel>(parameters => parameters
            .Add(component => component.Snapshot, snapshot)
            .Add(component => component.DefinitionType, JobType.Recurring));

        cut.FindAll(".job-health__outcomes > article").Should().HaveCount(6);
        foreach (var state in Enum.GetValues<JobExecutionState>())
        {
            cut.Find($".job-health__outcomes > article[data-state='{state.ToString().ToLowerInvariant()}']")
                .TextContent.Should().Contain($"ExecutionStates:{state}");
        }

        cut.FindAll(".job-health__score").Should().HaveCount(3);
        cut.FindAll(".job-health__duration > div").Should().HaveCount(6);
        cut.Markup.Should().Contain("JobDetail:Health:ExecutedCompletions");
        cut.Find(".job-health__score[data-tone='info'] strong").TextContent.Should().Contain("90");
        cut.FindAll(".job-health__score-label button").Should().HaveCount(2);

        var triggered = context.Render<JobHealthPanel>(parameters => parameters
            .Add(component => component.Snapshot, snapshot)
            .Add(component => component.DefinitionType, JobType.Triggered));

        triggered.Find(".job-health__score[data-tone='info'] strong").TextContent
            .Should().Be("Common:NotAvailable");
        triggered.Markup.Should().Contain("JobDetail:Health:TriggeredFulfillmentNotApplicable");
    }

    [Fact]
    public async Task ActivityPanel_ShouldUseBoundedClickableRowsWithoutNavigationLinks()
    {
        await using var context = new JobSchedulerUiTestContext();
        var template = context.RecurringDefinition.CreateExecutionTemplate();
        var running = CreateExecution(template, "running-instance-0001", JobExecutionState.Running);
        var failed = CreateExecution(template, "failed-instance-0001", JobExecutionState.Failed);
        string? selectedInstanceId = null;
        var cut = context.Render<JobExecutionActivityPanel>(parameters => parameters
            .Add(component => component.Running, new JobExecutionActivitySlice
            {
                Items = [running],
                TotalCount = 7,
                ItemLimit = 3
            })
            .Add(component => component.Failures, new JobExecutionActivitySlice
            {
                Items = [failed],
                TotalCount = 11,
                ItemLimit = 3
            })
            .Add(component => component.ObservedAtUtc, OBSERVED_AT)
            .Add(component => component.ExecutionSelected,
                EventCallback.Factory.Create<string>(this, instanceId => selectedInstanceId = instanceId)));

        var rows = cut.FindAll(".job-activity__item");
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(row => !row.HasAttribute("href"));
        cut.Find(".job-activity__stream--running > header > strong").TextContent.Should().Be("7");
        cut.Find(".job-activity__stream--failed > header > strong").TextContent.Should().Be("11");
        cut.Find(".job-activity__heading .mud-chip").TextContent.Should().Contain("3");

        rows[1].Click();

        selectedInstanceId.Should().Be(failed.InstanceId);
    }

    [Fact]
    public async Task DetailHeader_ShouldKeepCurrentQueuedEvidenceOutsideHealthWindow()
    {
        var clock = new ManualTimeProvider(OBSERVED_AT.AddDays(-31));
        await using var context = new JobSchedulerUiTestContext(timeProvider: clock);
        await context.Store.EnqueueAsync(new JobEnqueueRequest
        {
            SchedulerScopeKey = JobSchedulerUiTestContext.SCOPE,
            InstanceId = "queued-before-health-window-0001",
            JobKey = context.TriggeredDefinition.Declaration.JobKey,
            ExpectedOwnerId = context.TriggeredDefinition.OwnerId,
            ExpectedJobRevisionId = context.TriggeredDefinition.JobRevisionId,
            JobArgs = "{}"
        }, Xunit.TestContext.Current.CancellationToken);
        clock.SetUtcNow(OBSERVED_AT);

        var cut = context.Render<JobDefinitionDetailPage>(parameters => parameters
            .Add(page => page.JobKey, context.TriggeredDefinition.Declaration.JobKey));

        cut.WaitForAssertion(() =>
        {
            cut.Find(".job-detail-header__metric[data-metric='active'] dd").TextContent.Should().Be("1");
            cut.Find(".job-detail-header__metric[data-metric='queued'] dd").TextContent.Should().Be("1");
            cut.Find(".job-detail-header__metric[data-metric='running'] dd").TextContent.Should().Be("0");
            cut.Find(".job-health__outcomes > article[data-state='queued'] strong").TextContent.Should().Be("0");
        });
    }

    [Fact]
    public async Task DetailPage_ShouldOpenLatestExecutionInPlace()
    {
        await using var context = new JobSchedulerUiTestContext();
        var execution = await context.Store.EnqueueAsync(new JobEnqueueRequest
        {
            SchedulerScopeKey = JobSchedulerUiTestContext.SCOPE,
            InstanceId = "job-detail-dialog-0001",
            JobKey = context.TriggeredDefinition.Declaration.JobKey,
            ExpectedOwnerId = context.TriggeredDefinition.OwnerId,
            ExpectedJobRevisionId = context.TriggeredDefinition.JobRevisionId,
            JobArgs = "{}"
        }, Xunit.TestContext.Current.CancellationToken);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var originalUri = navigation.Uri;
        var cut = context.Render<JobDefinitionDetailPage>(parameters => parameters
            .Add(page => page.JobKey, context.TriggeredDefinition.Declaration.JobKey));

        cut.WaitForAssertion(() => cut.Find(".job-latest-panel__instance button").Should().NotBeNull());
        var openTask = cut.Find(".job-latest-panel__instance button").ClickAsync();

        context.DialogProvider.WaitForAssertion(() =>
        {
            context.DialogProvider.Markup.Should().Contain(execution.InstanceId);
            navigation.Uri.Should().Be(originalUri);
        });
        await context.DialogProvider.FindAll("button")
            .Single(button => button.TextContent.Contains("Common:Close", StringComparison.Ordinal))
            .ClickAsync();
        await openTask;
    }

    private static JobExecutionInstance CreateExecution(
        JobExecutionTemplate template,
        string instanceId,
        JobExecutionState state) => new()
    {
        InstanceId = instanceId,
        Template = template,
        Origin = JobExecutionOrigin.RecurringRunNow,
        AvailableAtUtc = OBSERVED_AT.AddMinutes(-10),
        State = state,
        CreatedAtUtc = OBSERVED_AT.AddMinutes(-10),
        StartedAtUtc = OBSERVED_AT.AddMinutes(-9),
        CompletedAtUtc = state == JobExecutionState.Running ? null : OBSERVED_AT.AddMinutes(-1),
        ExecutionAttempt = 1,
        RunningWorkerInstanceId = state == JobExecutionState.Running ? "worker-a-pod-1" : null
    };

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        internal void SetUtcNow(DateTimeOffset value) => _utcNow = value;
    }
}
