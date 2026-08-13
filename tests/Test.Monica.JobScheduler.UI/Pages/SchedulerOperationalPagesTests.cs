using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Results;
using Monica.JobScheduler.Models;
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
        });
    }

    [Fact]
    public async Task Catalog_ShouldRenderImmutableScheduleAndPolicyOnlyActions()
    {
        await using var context = new JobSchedulerUiTestContext();

        var cut = context.Render<JobCatalogPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("0 */5 * * * *");
            cut.Markup.Should().Contain("UTC");
            cut.FindAll("button").Should().HaveCountGreaterThanOrEqualTo(5);
        });
        cut.Markup.Should().NotContain("CronEditor");
    }

    [Fact]
    public async Task CatalogState_ShouldPauseAndResumeRecurringMaterializationWithoutChangingDeclaration()
    {
        await using var context = new JobSchedulerUiTestContext();
        await using var state = context.Services.GetRequiredService<JobCatalogPageStateFactory>().Create(20);
        await state.InitializeAsync();
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
    public async Task Executions_ShouldRenderDurablyQueuedWork()
    {
        await using var context = new JobSchedulerUiTestContext();
        var execution = await context.Store.EnqueueAsync(new JobEnqueueRequest
        {
            SchedulerScopeKey = JobSchedulerUiTestContext.SCOPE,
            InstanceId = "execution-ui-test-0001",
            JobKey = context.TriggeredDefinition.Declaration.JobKey,
            ExpectedOwnerId = context.TriggeredDefinition.OwnerId,
            ExpectedJobRevisionId = context.TriggeredDefinition.JobRevisionId,
            JobArgs = "{}",
            AvailableAtUtc = DateTimeOffset.UtcNow
        }, Xunit.TestContext.Current.CancellationToken);

        var cut = context.Render<JobExecutionsPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain(execution.InstanceId);
            cut.Markup.Should().Contain("ExecutionStates:Queued");
            cut.Markup.Should().Contain("Executions:Filters:States");
            cut.Markup.Should().Contain("Executions:Filters:TimeRange");
            cut.Markup.Should().Contain("Executions:Filters:SortBy");
            cut.Markup.Should().Contain("Executions:Filters:PageSize");
            cut.Markup.Should().Contain("Executions:Columns:Started");
            cut.Markup.Should().Contain("Executions:Columns:Completed");
            cut.Markup.Should().Contain("Executions:Columns:Duration");
        });
    }

    [Fact]
    public async Task ExecutionLedgerState_ShouldApplyMultiStateTimeSortAndPageSizeControls()
    {
        await using var context = new JobSchedulerUiTestContext();
        var execution = await context.Store.EnqueueAsync(new JobEnqueueRequest
        {
            SchedulerScopeKey = JobSchedulerUiTestContext.SCOPE,
            InstanceId = "execution-filter-test-0001",
            JobKey = context.TriggeredDefinition.Declaration.JobKey,
            ExpectedOwnerId = context.TriggeredDefinition.OwnerId,
            ExpectedJobRevisionId = context.TriggeredDefinition.JobRevisionId,
            JobArgs = "{}",
            AvailableAtUtc = DateTimeOffset.UtcNow
        }, Xunit.TestContext.Current.CancellationToken);
        var factory = context.Services.GetRequiredService<JobExecutionsStateFactory>();
        await using var state = factory.CreatePageState();

        state.SetSelectedStates([JobExecutionState.Succeeded, JobExecutionState.Failed]);
        await state.ApplyFiltersAsync();
        state.Executions.Should().BeEmpty();

        state.SetSelectedStates([JobExecutionState.Queued]);
        state.TimeRange = ExecutionTimeRange.Custom;
        state.CustomStartDate = DateTime.Today.AddDays(1);
        await state.ApplyFiltersAsync();
        state.Executions.Should().BeEmpty();

        await state.ResetFiltersAsync();
        await state.SetSortFieldAsync(JobExecutionSortField.AvailableAtUtc);
        await state.ToggleSortDirectionAsync();
        await state.SetPageSizeAsync(10);
        state.Executions.Should().ContainSingle(item => item.InstanceId == execution.InstanceId);
        state.SortField.Should().Be(JobExecutionSortField.AvailableAtUtc);
        state.SortDescending.Should().BeFalse();
        state.PageSize.Should().Be(10);
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
            provider.Markup.Should().Contain("ExecutionDetail:HistoryLogLevel");
            provider.Markup.Should().Contain("ExecutionDetail:HistoryWorker");
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

        state.SelectedStates.Should().BeEquivalentTo(
            [JobExecutionState.Queued, JobExecutionState.Running]);
        state.SearchText.Should().Be(execution.InstanceId);
        state.Executions.Should().ContainSingle(item => item.InstanceId == execution.InstanceId);
    }

    [Fact]
    public async Task ExecutionCancellation_ShouldReauthorizeBeforePersistingAndRefreshTheLedger()
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
        state.Executions.Should().ContainSingle(item =>
            item.InstanceId == execution.InstanceId && item.State == JobExecutionState.Cancelled);
    }
}
