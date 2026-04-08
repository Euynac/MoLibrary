using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Monica.JobScheduler.UI.Components.Monitor;
using MudBlazor;
using Test.Monica.JobScheduler.UI.Infrastructure;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Components.Monitor;

public class AutoRefreshControlTests
{
    [Fact]
    public async Task Render_WhenManualRefreshButtonIsClicked_ShouldInvokeCallback()
    {
        await using var context = new JobSchedulerUiTestContext();
        var refreshCalls = 0;
        context.Render<MudPopoverProvider>();

        var cut = context.Render<AutoRefreshControl>(parameters => parameters
            .Add(component => component.OnRefreshRequested, EventCallback.Factory.Create(this, () => refreshCalls++)));

        var button = cut.FindAll("button")
            .Single(element => element.TextContent.Contains("Monitor:AutoRefresh:ManualRefresh", StringComparison.Ordinal));

        button.Click();

        cut.WaitForAssertion(() => refreshCalls.Should().Be(1));
    }
}
