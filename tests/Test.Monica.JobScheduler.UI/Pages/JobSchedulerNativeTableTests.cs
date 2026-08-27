using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.UI.Pages;
using MudBlazor;
using Test.Monica.JobScheduler.UI.Infrastructure;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Pages;

public sealed class JobSchedulerNativeTableTests
{
    private static readonly DateTimeOffset CATALOG_NOW =
        new(2026, 8, 14, 1, 2, 3, 456, TimeSpan.Zero);

    [Fact]
    public async Task Catalog_WhenLoaded_ShouldRenderNativeTableWithFocusedOperatorActions()
    {
        await using var context = new JobSchedulerUiTestContext();

        var cut = context.Render<JobCatalogPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".catalog-table__scroll table");
            cut.Markup.Should().Contain("Recurring cleanup");
            cut.Markup.Should().Contain("0 */5 * * * *");
        });

        var table = cut.Find(".catalog-table__scroll table");
        table.QuerySelector("thead").Should().NotBeNull();
        table.QuerySelector("tbody").Should().NotBeNull();
        table.TextContent.Should().Contain("Catalog:Columns:Definition");
        table.TextContent.Should().Contain("Catalog:Filters:Owner");
        table.TextContent.Should().Contain("Catalog:Columns:Lifecycle");
        table.TextContent.Should().Contain("Catalog:Columns:Schedule");
        table.TextContent.Should().Contain("Catalog:Columns:NextRun");
        table.TextContent.Should().Contain("Catalog:Columns:LastRun");
        table.TextContent.Should().Contain("Catalog:Columns:Actions");
        cut.FindAll(".catalog-item").Should().BeEmpty();
        table.QuerySelector("th.catalog-table__definition-heading").Should().NotBeNull();

        var schedule = table.QuerySelector(".catalog-table__schedule");
        schedule.Should().NotBeNull();
        schedule!.TextContent.Should().Contain("0 */5 * * * *");
        // The deployment timezone is deliberately not repeated in the catalog row.
        schedule.QuerySelector(".catalog-table__timezone").Should().BeNull();
        schedule.TextContent.Should().NotContain("Catalog:Cron:Parsing");
        table.QuerySelector("time.catalog-table__next-run")
            .Should().NotBeNull();

        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var originalUri = navigation.Uri;
        table.QuerySelector(".catalog-table__owner")!.Click();
        navigation.Uri.Should().Be(originalUri);

        // The definition title links straight into the operational dossier.
        var titleLink = table.QuerySelector("a.catalog-table__identity-name");
        titleLink.Should().NotBeNull();
        titleLink!.GetAttribute("href").Should().Be("/job-scheduler/catalog/worker-a/Sample.Jobs.RecurringCleanup");

        // The job key copies its full value instead of navigating.
        var keyButton = table.QuerySelector("button.catalog-table__identity-key");
        keyButton.Should().NotBeNull();
        keyButton!.GetAttribute("aria-label").Should().Be("Catalog:Actions:CopyKey");

        var filter = cut.Find(".catalog-filter");
        filter.TextContent.Should().Contain("Catalog:Filters:Policy");
        filter.TextContent.Should().Contain("Catalog:Filters:AnyPolicy");
        filter.TextContent.Should().NotContain("Catalog:Filters:State");
        filter.TextContent.Should().NotContain("Catalog:Filters:PageSize");
        cut.Markup.Should().Contain("Catalog:Filters:PageSize");

        var actions = cut.Find(".catalog-table__actions");
        actions.QuerySelector("[aria-label='Catalog:Actions:Pause']").Should().NotBeNull();
        actions.QuerySelector("[aria-label='Catalog:Actions:Run']").Should().NotBeNull();
        actions.QuerySelector("[aria-label='Catalog:Actions:Details']").Should().NotBeNull();
    }

    [Fact]
    public async Task CatalogNextRun_ShouldUseSchedulerTimeAndKeepRelativeTimingInTheTooltip()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        await using var context = new JobSchedulerUiTestContext(
            schedulerTimeZone: timeZone,
            timeProvider: new FixedTimeProvider(CATALOG_NOW));

        var cut = context.Render<JobCatalogPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("time.catalog-table__next-run").TextContent.Should()
                .Contain("2026-08-14 09:05:00.000");
            cut.FindComponents<MudTooltip>()
                .Any(tooltip => tooltip.Instance.Text?.Contains(
                    "Catalog:NextRun:In",
                    StringComparison.Ordinal) == true)
                .Should().BeTrue();
        });
    }

    [Fact]
    public async Task Executions_WhenLoaded_ShouldRenderNativeTableAndEveryStateChip()
    {
        await using var context = new JobSchedulerUiTestContext();
        var execution = await context.Store.EnqueueAsync(new JobEnqueueRequest
        {
            SchedulerScopeKey = JobSchedulerUiTestContext.SCOPE,
            InstanceId = "execution-native-table-0001",
            JobKey = context.TriggeredDefinition.Declaration.JobKey,
            OwnerKey = context.TriggeredDefinition.OwnerKey,
            JobArgs = "{}",
            AvailableAtUtc = DateTimeOffset.UtcNow
        }, Xunit.TestContext.Current.CancellationToken);

        var cut = context.Render<JobExecutionsPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".executions-page__table table");
            cut.Markup.Should().Contain(execution.InstanceId);
        });

        var table = cut.Find(".executions-page__table table");
        table.QuerySelector("thead").Should().NotBeNull();
        table.QuerySelector("tbody").Should().NotBeNull();
        table.TextContent.Should().Contain("Executions:Columns:Definition");
        table.TextContent.Should().Contain("Executions:Columns:State");
        table.TextContent.Should().Contain("Executions:Columns:Created");
        table.TextContent.Should().Contain("Executions:Columns:Duration");
        // The job title opens the execution detail evidence.
        table.QuerySelector("button.execution-table__name").Should().NotBeNull();
        cut.FindAll(".execution-activity__item").Should().BeEmpty();

        var stateChips = cut.Find(".executions-page__state-chips");
        foreach (var state in Enum.GetValues<JobExecutionState>())
        {
            stateChips.TextContent.Should().Contain($"ExecutionStates:{state}");
        }

        stateChips.TextContent.Should().Contain("ExecutionStates:Skipped");

        var filterGrid = cut.Find(".executions-page__filter-grid");
        filterGrid.TextContent.Should().NotContain("Executions:Filters:SortBy");
        filterGrid.TextContent.Should().NotContain("Executions:Filters:PageSize");
        cut.Markup.Should().NotContain("Executions:Filters:SortBy");
        cut.Markup.Should().Contain("Executions:Filters:PageSize");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
