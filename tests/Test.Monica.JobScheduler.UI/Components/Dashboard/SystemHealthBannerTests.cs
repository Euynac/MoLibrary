using AwesomeAssertions;
using Bunit;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.UI.Components.Dashboard;
using Test.Monica.JobScheduler.UI.Infrastructure;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Components.Dashboard;

public class SystemHealthBannerTests
{
    [Fact]
    public async Task Render_WhenStatusIsDegraded_ShouldRenderMatchingClassAndLocalizedKey()
    {
        await using var context = new JobSchedulerUiTestContext();

        var cut = context.Render<SystemHealthBanner>(parameters => parameters
            .Add(component => component.Status, SystemHealthStatus.Degraded)
            .Add(component => component.Message, "degraded message"));

        cut.Markup.Should().Contain("health-degraded");
        cut.Markup.Should().Contain("Dashboard:SystemHealth:Degraded");
        cut.Markup.Should().Contain("degraded message");
    }
}
