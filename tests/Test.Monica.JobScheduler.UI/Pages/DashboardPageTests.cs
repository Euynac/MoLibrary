using AwesomeAssertions;
using Bunit;
using Monica.JobScheduler.UI.Pages;
using Test.Monica.JobScheduler.UI.Infrastructure;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Pages;

public class DashboardPageTests
{
    [Fact]
    public async Task Render_WhenFacadeReturnsFailure_ShouldShowErrorAndRetryAction()
    {
        await using var context = new JobSchedulerUiTestContext();
        context.AddDashboardFacade(JobSchedulerUiTestContext.CreateFailingDashboardFacade("cache exploded"));

        var cut = context.Render<DashboardPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Failed to build dashboard snapshot");
            cut.Markup.Should().Contain("cache exploded");
        });
        cut.Markup.Should().Contain("Common:Actions:Retry");
    }
}
