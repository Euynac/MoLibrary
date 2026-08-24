using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models.Operations;
using Monica.JobScheduler.UI.UIJobScheduler.Components;
using Test.Monica.JobScheduler.UI.Infrastructure;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Pages;

public sealed class OwnerConvergencePanelTests
{
    [Fact]
    public async Task Panel_ShouldRenderCompactOwnerItemsInsteadOfATable()
    {
        await using var context = new JobSchedulerUiTestContext();
        var overview = await LoadOverviewAsync(context);

        var cut = context.Render<OwnerConvergencePanel>(parameters => parameters
            .Add(component => component.Overview, overview));

        cut.FindAll(".owner-panel table").Should().BeEmpty();
        var ownerItem = cut.FindAll(".owner-panel__item").Should().ContainSingle().Which;
        ownerItem.TagName.Should().Be("BUTTON");
        ownerItem.TextContent.Should().Contain(JobSchedulerUiTestContext.OWNER);
        ownerItem.QuerySelector(".owner-panel__revision")!.TextContent
            .Should().Be(JobSchedulerUiTestContext.WORKER_REVISION);
        ownerItem.QuerySelector(".owner-panel__signal")!.TextContent
            .Should().Contain("Overview:Owners:Published");
        ownerItem.QuerySelectorAll(".owner-panel__metric strong")
            .Select(element => element.TextContent)
            .Should().Equal("1", "2");
    }

    [Fact]
    public async Task Panel_WhenOwnerIsClicked_ShouldOpenTheOwnerEvidenceDialog()
    {
        await using var context = new JobSchedulerUiTestContext();
        var overview = await LoadOverviewAsync(context);
        var cut = context.Render<OwnerConvergencePanel>(parameters => parameters
            .Add(component => component.Overview, overview));

        var openTask = cut.Find(".owner-panel__item").ClickAsync();
        var provider = context.DialogProvider;
        provider.WaitForAssertion(() => provider.FindAll(".owner-detail").Should().ContainSingle());

        provider.Markup.Should().Contain(JobSchedulerUiTestContext.OWNER);
        provider.Markup.Should().Contain(JobSchedulerUiTestContext.WORKER_REVISION);
        provider.Find("#owner-readiness-title").TextContent
            .Should().Be("Overview:Owners:Detail:Ready");
        provider.Markup.Should().Contain("Overview:Owners:Detail:ReadyDescription");
        provider.Markup.Should().Contain("Overview:Owners:Published");
        provider.Markup.Should().Contain("release-1");
        provider.FindAll(".owner-detail__signals article").Should().HaveCount(3);
        provider.FindAll(".owner-detail__job-list article").Should().HaveCount(2);
        provider.Markup.Should().Contain("Recurring cleanup");
        provider.Markup.Should().Contain("Generate report");
        provider.Markup.Should().Contain("JobTypes:Recurring");
        provider.Markup.Should().Contain("JobTypes:Triggered");

        await provider.FindAll("button")
            .Single(button => button.TextContent.Contains("Common:Close", StringComparison.Ordinal))
            .ClickAsync();
        await openTask;
    }

    private static async Task<JobSchedulerOverview> LoadOverviewAsync(JobSchedulerUiTestContext context)
    {
        var result = await context.Services
            .GetRequiredService<JobSchedulerFacade>()
            .GetOverviewAsync(Xunit.TestContext.Current.CancellationToken);
        result.Data.Should().NotBeNull();
        return result.Data!;
    }
}
