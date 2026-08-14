using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Results;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Models.Operations;
using Monica.JobScheduler.UI.Components;
using Monica.JobScheduler.UI.Pages;
using Monica.JobScheduler.UI.UIJobScheduler.Executions.State;
using Monica.JobScheduler.UI.UIJobScheduler.Components;
using Monica.JobScheduler.UI.UIJobScheduler.State;
using MudBlazor;
using Test.Monica.JobScheduler.UI.Infrastructure;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Pages;

public sealed class SchedulerOperationalPagesTests
{
    [Fact]
    public async Task DirectRoute_WhenCircuitIsUnauthorized_ShouldDenyBeforeLoadingSchedulerData()
    {
        await using var context = new JobSchedulerUiTestContext(isAuthorized: false);

        var cut = context.Render<JobCatalogPage>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Access:DeniedTitle"));
        cut.Markup.Should().NotContain(JobSchedulerUiTestContext.OWNER);
    }

    [Fact]
    public async Task Overview_ShouldExposeReleaseIntentAndWorkerConvergence()
    {
        await using var context = new JobSchedulerUiTestContext();

        var cut = context.Render<SchedulerOverviewPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("release-1");
            cut.Markup.Should().Contain(JobSchedulerUiTestContext.OWNER);
            cut.Markup.Should().Contain("Overview:Release:Converged");
            cut.Markup.Should().Contain("Overview:Workload:TotalJobs");
            cut.Markup.Should().Contain("Overview:Attention:Title");
            cut.Markup.Should().Contain("execution-flow__chart");
        });
    }

    [Fact]
    public async Task Catalog_ShouldSeparateRecurringAndTriggeredViews()
    {
        await using var context = new JobSchedulerUiTestContext();

        var cut = context.Render<JobCatalogPage>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Recurring cleanup"));
        cut.Markup.Should().NotContain("Generate report");

        cut.FindAll("button")
            .Single(button => button.TextContent.Contains("JobTypes:Triggered", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Generate report"));
        cut.Markup.Should().NotContain("Recurring cleanup");
    }

    [Fact]
    public async Task CatalogOwnerFacets_DuringRollingRelease_ShouldDescribeTheActiveCatalog()
    {
        await using var context = new JobSchedulerUiTestContext();
        await context.Store.StageReleaseAsync(new JobCatalogReleaseStage(
            new JobCatalogReleaseManifest(
                JobSchedulerUiTestContext.SCOPE,
                "release-2",
                [new JobCatalogOwnerManifest(JobSchedulerUiTestContext.OWNER, "sha256:worker-b")]),
            2), Xunit.TestContext.Current.CancellationToken);
        await using var state = context.Services.GetRequiredService<JobCatalogPageStateFactory>().Create(20);

        await state.InitializeAsync();

        state.OwnerFacets.Should().ContainSingle()
            .Which.Should().Be(KeyValuePair.Create(JobSchedulerUiTestContext.OWNER, 2));
    }

    [Fact]
    public async Task CatalogState_ShouldPauseAndResumeRecurringMaterializationWithoutChangingDeclaration()
    {
        await using var context = new JobSchedulerUiTestContext();
        await using var state = context.Services.GetRequiredService<JobCatalogPageStateFactory>().Create(20);
        await state.InitializeAsync();
        await state.LoadTableAsync(new TableState
        {
            PageSize = 20,
            SortLabel = nameof(JobCatalogSortField.JobName),
            SortDirection = SortDirection.Ascending
        }, Xunit.TestContext.Current.CancellationToken);
        var recurring = state.Summaries.Single(summary =>
            summary.Definition.Declaration.JobType == JobType.Recurring);

        (await state.SetDisabledAsync(recurring, true)).Status.Should().Be(ResStatus.Ok);
        state.Summaries.Single(summary => summary.Definition.Declaration.JobKey ==
                                        recurring.Definition.Declaration.JobKey)
            .RecurringScheduleStatus.Should().Be(JobRecurringScheduleStatus.Suspended);

        var persisted = await context.Store.GetActiveDefinitionAsync(
            JobSchedulerUiTestContext.SCOPE,
            recurring.Definition.Declaration.JobKey,
            Xunit.TestContext.Current.CancellationToken);
        persisted!.IsDisabled.Should().BeTrue();
        persisted.Declaration.CronExpression.Should().Be("0 */5 * * * *");
    }

    [Fact]
    public async Task CatalogState_ShouldAllowRunNowWhileRecurringMaterializationIsPaused()
    {
        await using var context = new JobSchedulerUiTestContext();
        await using var state = context.Services.GetRequiredService<JobCatalogPageStateFactory>().Create(20);
        await state.InitializeAsync();
        await state.LoadTableAsync(new TableState
        {
            PageSize = 20,
            SortLabel = nameof(JobCatalogSortField.JobName),
            SortDirection = SortDirection.Ascending
        }, Xunit.TestContext.Current.CancellationToken);
        var recurring = state.Summaries.Single();
        (await state.SetDisabledAsync(recurring, true)).Status.Should().Be(ResStatus.Ok);
        recurring = state.Summaries.Single();

        var result = await state.RunRecurringNowAsync(recurring);

        result.Status.Should().Be(ResStatus.Ok);
        result.Data.Should().NotBeNull();
        result.Data!.Origin.Should().Be(JobExecutionOrigin.RecurringRunNow);
        result.Data.State.Should().Be(JobExecutionState.Queued);
    }

    [Fact]
    public async Task CatalogState_WhenDebugModeAloneSuppressesMaterialization_ShouldRejectQuickPauseAndAllowRunNow()
    {
        await using var context = new JobSchedulerUiTestContext();
        await SetDebugSuppressionAsync(context);
        await using var state = context.Services.GetRequiredService<JobCatalogPageStateFactory>().Create(20);
        await state.InitializeAsync();
        await state.LoadTableAsync(new TableState
        {
            PageSize = 20,
            SortLabel = nameof(JobCatalogSortField.JobName),
            SortDirection = SortDirection.Ascending
        }, Xunit.TestContext.Current.CancellationToken);
        var recurring = state.Summaries.Single();
        recurring.SuspensionReasons.Should().Be(JobRecurringScheduleSuspensionReason.DebugMode);

        var pause = await state.SetDisabledAsync(recurring, true);
        pause.IsFailed(out _).Should().BeTrue();

        state.ToggleSelection(recurring);
        var batchPause = await state.SetSelectedDisabledAsync(true);
        batchPause.IsFailed(out _).Should().BeTrue();

        var runNow = await state.RunRecurringNowAsync(recurring);
        runNow.Status.Should().Be(ResStatus.Ok);
        var persisted = await context.Store.GetActiveDefinitionAsync(
            JobSchedulerUiTestContext.SCOPE,
            recurring.Definition.Declaration.JobKey,
            Xunit.TestContext.Current.CancellationToken);
        persisted!.IsDisabled.Should().BeFalse();
    }

    [Fact]
    public async Task Catalog_WhenDebugModeSuppressesMaterialization_ShouldShowManualOnlyAndKeepActionsDiscoverable()
    {
        await using var context = new JobSchedulerUiTestContext();
        await SetDebugSuppressionAsync(context);

        var cut = context.Render<JobCatalogPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Catalog:Lifecycle:DebugSuppressed");
            var actions = cut.Find(".catalog-table__actions");
            actions.QuerySelector("[aria-label='Catalog:Actions:Pause']")!
                .HasAttribute("disabled").Should().BeFalse();
            actions.QuerySelector("[aria-label='Catalog:Actions:Run']")!
                .HasAttribute("disabled").Should().BeFalse();
        });
    }

    [Fact]
    public async Task CatalogState_WhenPolicyAndDebugBothSuppressMaterialization_ShouldResumeOnlyOperatorPolicy()
    {
        await using var context = new JobSchedulerUiTestContext();
        await using var state = context.Services.GetRequiredService<JobCatalogPageStateFactory>().Create(20);
        await state.InitializeAsync();
        await state.LoadTableAsync(new TableState
        {
            PageSize = 20,
            SortLabel = nameof(JobCatalogSortField.JobName),
            SortDirection = SortDirection.Ascending
        }, Xunit.TestContext.Current.CancellationToken);
        var recurring = state.Summaries.Single();
        (await state.SetDisabledAsync(recurring, true)).Status.Should().Be(ResStatus.Ok);
        await SetDebugSuppressionAsync(context);
        await state.LoadTableAsync(new TableState
        {
            PageSize = 20,
            SortLabel = nameof(JobCatalogSortField.JobName),
            SortDirection = SortDirection.Ascending
        }, Xunit.TestContext.Current.CancellationToken);
        recurring = state.Summaries.Single();
        recurring.SuspensionReasons.Should().Be(
            JobRecurringScheduleSuspensionReason.OperatorPolicy | JobRecurringScheduleSuspensionReason.DebugMode);

        (await state.SetDisabledAsync(recurring, false)).Status.Should().Be(ResStatus.Ok);

        var optimistic = state.Summaries.Single();
        optimistic.SuspensionReasons.Should().Be(JobRecurringScheduleSuspensionReason.DebugMode);
        optimistic.RecurringScheduleStatus.Should().Be(JobRecurringScheduleStatus.Suspended);
    }

    [Fact]
    public async Task JobDetail_WhenDebugModeSuppressesMaterialization_ShouldExplainManualOnlyLifecycle()
    {
        await using var context = new JobSchedulerUiTestContext();
        await SetDebugSuppressionAsync(context);

        var cut = context.Render<JobDefinitionDetailPage>(parameters => parameters
            .Add(page => page.JobKey, context.RecurringDefinition.Declaration.JobKey));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Catalog:Policy:Enabled");
            cut.Markup.Should().Contain("Catalog:Lifecycle:DebugSuppressed");
            cut.Markup.Should().Contain("JobDetail:Schedule:DebugManualOnly");
        });
        var pause = cut.FindAll("button").Single(button =>
            button.TextContent.Contains("Catalog:Actions:Pause", StringComparison.Ordinal));
        pause.HasAttribute("disabled").Should().BeFalse();
        var runNow = cut.FindAll("button").Single(button =>
            button.TextContent.Contains("Catalog:Actions:Run", StringComparison.Ordinal));
        runNow.HasAttribute("disabled").Should().BeFalse();

        pause.Click();

        var persisted = await context.Store.GetActiveDefinitionAsync(
            JobSchedulerUiTestContext.SCOPE,
            context.RecurringDefinition.Declaration.JobKey,
            Xunit.TestContext.Current.CancellationToken);
        persisted!.IsDisabled.Should().BeFalse();
    }

    [Fact]
    public async Task OverviewRecentActivity_ShouldRenderClickableTimelineInsteadOfTableActions()
    {
        await using var context = new JobSchedulerUiTestContext();
        var execution = await context.Store.EnqueueAsync(new JobEnqueueRequest
        {
            SchedulerScopeKey = JobSchedulerUiTestContext.SCOPE,
            InstanceId = "overview-activity-0001",
            JobKey = context.TriggeredDefinition.Declaration.JobKey,
            ExpectedOwnerId = context.TriggeredDefinition.OwnerId,
            ExpectedJobRevisionId = context.TriggeredDefinition.JobRevisionId,
            JobArgs = "{}"
        }, Xunit.TestContext.Current.CancellationToken);

        var cut = context.Render<SchedulerOverviewPage>();

        cut.WaitForAssertion(() =>
        {
            var activity = cut.Find(".recent-panel__event-link");
            activity.GetAttribute("href").Should().Contain(execution.InstanceId);
            cut.FindAll(".recent-panel table").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task Statistics_ShouldRenderBoundedReliabilityTrendDurationAndRankings()
    {
        await using var context = new JobSchedulerUiTestContext();

        var cut = context.Render<SchedulerStatisticsPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Analytics:Metrics:Reliability");
            cut.Markup.Should().Contain("Analytics:Trend:Title");
            cut.Markup.Should().Contain("Analytics:Duration:Title");
            cut.Markup.Should().Contain("Analytics:Rankings:Title");
            cut.Markup.Should().Contain("analytics-dashboard__bars");
        });
    }

    [Fact]
    public async Task JobDetail_ShouldRenderSchedulePolicyRevisionAndLatestExecutionEvidence()
    {
        await using var context = new JobSchedulerUiTestContext();

        var cut = context.Render<JobDefinitionDetailPage>(parameters => parameters
            .Add(page => page.JobKey, context.RecurringDefinition.Declaration.JobKey));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain(context.RecurringDefinition.Declaration.JobKey);
            cut.Markup.Should().Contain("JobDetail:Schedule:NextOccurrence");
            cut.Markup.Should().Contain("JobDetail:Contract:PolicyTitle");
            cut.Markup.Should().Contain("JobDetail:Contract:JobRevision");
            cut.Markup.Should().Contain("RecurringScheduleStatuses:Scheduled");
            cut.Markup.Should().Contain("JobDetail:Health:Title");
        });
    }

    [Fact]
    public async Task CronInspector_ShouldSupportLocalDraftValidationWithoutMutatingTheCatalog()
    {
        await using var context = new JobSchedulerUiTestContext();
        var summary = (await context.Store.GetOperationalSummaryAsync(
            JobSchedulerUiTestContext.SCOPE,
            context.RecurringDefinition.Declaration.JobKey,
            Xunit.TestContext.Current.CancellationToken))!;
        var dialogService = context.Services.GetRequiredService<IDialogService>();
        await dialogService.ShowAsync<CronScheduleInspectorDialog>(
            "Cron",
            new DialogParameters<CronScheduleInspectorDialog>
            {
                { dialog => dialog.Definition, context.RecurringDefinition },
                { dialog => dialog.OperationalSummary, summary }
            });

        var provider = context.DialogProvider;
        provider.WaitForAssertion(() =>
        {
            provider.Markup.Should().Contain("CronInspector:Expression:Draft");
            provider.Markup.Should().Contain("CronInspector:Preview:ScheduleTime");
            provider.Markup.Should().Contain(context.RecurringDefinition.Declaration.CronExpression);
        });
        provider.FindAll("input").First().Input("not-a-cron");
        provider.WaitForAssertion(() => provider.Markup.Should().Contain("CronInspector:Preview:Unavailable"));

        var persisted = await context.Store.GetActiveDefinitionAsync(
            JobSchedulerUiTestContext.SCOPE,
            context.RecurringDefinition.Declaration.JobKey,
            Xunit.TestContext.Current.CancellationToken);
        persisted!.Declaration.CronExpression.Should().Be("0 */5 * * * *");
    }

    [Fact]
    public async Task PolicyDialog_ShouldExposeOnlyOperatorOwnedFields()
    {
        await using var context = new JobSchedulerUiTestContext();

        var dialogService = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<JobPolicyDialog>
        {
            { dialog => dialog.Definition, context.TriggeredDefinition }
        };
        await dialogService.ShowAsync<JobPolicyDialog>("Policy", parameters);

        var provider = context.DialogProvider;
        provider.WaitForAssertion(() => provider.Markup.Should().Contain("Policy:DisabledOverride"));
        provider.Markup.Should().Contain("Policy:MaxRecords");
        provider.Markup.Should().Contain("Policy:MaxDays");
        provider.Markup.Should().NotContain("CronExpression");
        provider.Markup.Should().NotContain("MaxConcurrency");
    }

    [Fact]
    public async Task PolicyDialog_WhenAccessIsRevokedBeforeSave_ShouldRejectTheMutation()
    {
        await using var context = new JobSchedulerUiTestContext();
        var dialogService = context.Services.GetRequiredService<IDialogService>();
        await dialogService.ShowAsync<JobPolicyDialog>(
            "Policy",
            new DialogParameters<JobPolicyDialog>
            {
                { dialog => dialog.Definition, context.TriggeredDefinition }
            });
        context.Access.IsAuthorized = false;

        var provider = context.DialogProvider;
        provider.WaitForElement("button");
        provider.FindAll("button")
            .Single(button => button.TextContent.Contains("Common:Save", StringComparison.Ordinal))
            .Click();

        provider.WaitForAssertion(() => provider.Markup.Should().Contain("Access:DeniedDescription"));
        var definition = await context.Store.GetActiveDefinitionAsync(
            JobSchedulerUiTestContext.SCOPE,
            context.TriggeredDefinition.Declaration.JobKey,
            Xunit.TestContext.Current.CancellationToken);
        definition!.Policy.ConcurrencyStamp.Should().Be(context.TriggeredDefinition.Policy.ConcurrencyStamp);
    }

    [Fact]
    public async Task ExecutionDetail_ShouldRenderTimingPolicyLeaseHistoryAndDiagnosticActions()
    {
        await using var context = new JobSchedulerUiTestContext();
        var execution = await context.Store.EnqueueAsync(new JobEnqueueRequest
        {
            SchedulerScopeKey = JobSchedulerUiTestContext.SCOPE,
            InstanceId = "execution-detail-test-0001",
            JobKey = context.TriggeredDefinition.Declaration.JobKey,
            ExpectedOwnerId = context.TriggeredDefinition.OwnerId,
            ExpectedJobRevisionId = context.TriggeredDefinition.JobRevisionId,
            JobArgs = "{\"report\":\"daily\"}",
            AvailableAtUtc = DateTimeOffset.UtcNow
        }, Xunit.TestContext.Current.CancellationToken);
        var dialogService = context.Services.GetRequiredService<IDialogService>();
        await dialogService.ShowAsync<ExecutionDetailDialog>(
            "Execution",
            new DialogParameters<ExecutionDetailDialog>
            {
                { dialog => dialog.InstanceId, execution.InstanceId }
            });

        var provider = context.DialogProvider;
        provider.WaitForAssertion(() =>
        {
            provider.Markup.Should().Contain(execution.InstanceId);
            provider.Markup.Should().Contain("ExecutionDetail:Identity");
            provider.Markup.Should().Contain("ExecutionDetail:PolicySnapshot");
            provider.Markup.Should().Contain("ExecutionDetail:Timing");
            provider.Markup.Should().Contain("ExecutionDetail:LeaseLosses");
            provider.Markup.Should().Contain("execution-history__timeline");
            provider.Markup.Should().Contain("ExecutionDetail:Actions:CopyDiagnostics");
            provider.Markup.Should().Contain("ExecutionDetail:Actions:ViewCatalog");
        });
    }

    [Fact]
    public async Task ExecutionLedgerState_WhenDisposedDuringAuthorization_ShouldCancelAndJoinTheLoad()
    {
        await using var context = new JobSchedulerUiTestContext();
        context.Access.BlockAuthorization();
        var state = context.Services.GetRequiredService<JobExecutionsStateFactory>().CreatePageState();

        var load = state.InitializeAsync();
        await context.Access.AuthorizationStarted.WaitAsync(Xunit.TestContext.Current.CancellationToken);
        await state.DisposeAsync();
        await load;
    }

    [Fact]
    public async Task ExecutionsDeepLink_ShouldApplyStatesAndRequestedInstanceFilter()
    {
        await using var context = new JobSchedulerUiTestContext();
        var execution = await context.Store.EnqueueAsync(new JobEnqueueRequest
        {
            SchedulerScopeKey = JobSchedulerUiTestContext.SCOPE,
            InstanceId = "execution-deep-link-0001",
            JobKey = context.TriggeredDefinition.Declaration.JobKey,
            ExpectedOwnerId = context.TriggeredDefinition.OwnerId,
            ExpectedJobRevisionId = context.TriggeredDefinition.JobRevisionId,
            JobArgs = "{}",
            AvailableAtUtc = DateTimeOffset.UtcNow
        }, Xunit.TestContext.Current.CancellationToken);

        await using var state = context.Services.GetRequiredService<JobExecutionsStateFactory>().CreatePageState();
        state.ApplyInitialQuery("queued,Running,invalid", execution.InstanceId);
        await state.InitializeAsync();
        await state.LoadTableAsync(new TableState
        {
            PageSize = 20,
            SortLabel = nameof(JobExecutionSortField.CreatedAtUtc),
            SortDirection = SortDirection.Descending
        }, Xunit.TestContext.Current.CancellationToken);

        state.SelectedStates.Should().BeEquivalentTo(
            [JobExecutionState.Queued, JobExecutionState.Running]);
        state.SearchText.Should().Be(execution.InstanceId);
        state.Executions.Should().ContainSingle(item => item.InstanceId == execution.InstanceId);
    }

    [Fact]
    public async Task ExecutionCancellation_ShouldReauthorizeBeforePersistingAndReloadThroughTableState()
    {
        await using var context = new JobSchedulerUiTestContext();
        var execution = await context.Store.EnqueueAsync(new JobEnqueueRequest
        {
            SchedulerScopeKey = JobSchedulerUiTestContext.SCOPE,
            InstanceId = "execution-cancel-test-0001",
            JobKey = context.TriggeredDefinition.Declaration.JobKey,
            ExpectedOwnerId = context.TriggeredDefinition.OwnerId,
            ExpectedJobRevisionId = context.TriggeredDefinition.JobRevisionId,
            JobArgs = "{}",
            AvailableAtUtc = DateTimeOffset.UtcNow
        }, Xunit.TestContext.Current.CancellationToken);
        await using var state = context.Services.GetRequiredService<JobExecutionsStateFactory>().CreatePageState();
        await state.InitializeAsync();

        context.Access.IsAuthorized = false;
        var denied = await state.RequestCancellationAsync(execution.InstanceId);
        denied.IsAuthorized.Should().BeFalse();
        var unchanged = await context.Store.GetExecutionAsync(
            JobSchedulerUiTestContext.SCOPE,
            execution.InstanceId,
            Xunit.TestContext.Current.CancellationToken);
        unchanged!.State.Should().Be(JobExecutionState.Queued);

        context.Access.IsAuthorized = true;
        var applied = await state.RequestCancellationAsync(execution.InstanceId);
        applied.Status.Should().Be(JobCancellationStatus.Cancelled);
        await state.LoadTableAsync(new TableState
        {
            PageSize = 20,
            SortLabel = nameof(JobExecutionSortField.CreatedAtUtc),
            SortDirection = SortDirection.Descending
        }, Xunit.TestContext.Current.CancellationToken);
        state.Executions.Should().ContainSingle(item =>
            item.InstanceId == execution.InstanceId && item.State == JobExecutionState.Cancelled);
    }

    private static async Task SetDebugSuppressionAsync(JobSchedulerUiTestContext context)
    {
        var catalog = await context.Store.GetActiveCatalogAsync(
            JobSchedulerUiTestContext.SCOPE,
            Xunit.TestContext.Current.CancellationToken);
        await context.Store.SynchronizeRecurringScheduleAsync(
            new RecurringScheduleSynchronization
            {
                Template = context.RecurringDefinition.CreateExecutionTemplate(),
                Schedule = new RecurringScheduleDefinition
                {
                    CronExpression = context.RecurringDefinition.Declaration.CronExpression!,
                    TimeZoneId = context.RecurringDefinition.Declaration.TimeZoneId!,
                    StartTimeUtc = context.RecurringDefinition.Declaration.StartTimeUtc,
                    EndTimeUtc = context.RecurringDefinition.Declaration.EndTimeUtc
                },
                ChangeEpoch = catalog!.Version.ChangeEpoch,
                SuspensionReasons = JobRecurringScheduleSuspensionReason.DebugMode
            },
            Xunit.TestContext.Current.CancellationToken);
    }
}
