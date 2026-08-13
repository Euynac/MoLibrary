using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.UI.Components;
using Monica.JobScheduler.UI.Pages;
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
            cut.Markup.Should().Contain(execution.InstanceId[..12]);
            cut.Markup.Should().Contain("ExecutionStates:Queued");
        });
    }
}
