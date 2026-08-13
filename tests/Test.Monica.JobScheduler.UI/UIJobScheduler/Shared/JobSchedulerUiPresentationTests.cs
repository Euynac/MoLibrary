using AwesomeAssertions;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;
using MudBlazor;
using Xunit;

namespace Test.Monica.JobScheduler.UI.UIJobScheduler.Shared;

public sealed class JobSchedulerUiPresentationTests
{
    [Theory]
    [InlineData(JobExecutionState.Queued, Color.Info)]
    [InlineData(JobExecutionState.Running, Color.Primary)]
    [InlineData(JobExecutionState.Succeeded, Color.Success)]
    [InlineData(JobExecutionState.Failed, Color.Error)]
    [InlineData(JobExecutionState.Cancelled, Color.Warning)]
    public void GetStateColor_ShouldUseSemanticMudColor(JobExecutionState state, Color expected)
    {
        JobSchedulerUiPresentation.GetStateColor(state).Should().Be(expected);
    }

    [Fact]
    public void ShortIdentity_ShouldBoundDiagnosticIdentityWithoutChangingShortValues()
    {
        JobSchedulerUiPresentation.ShortIdentity("short").Should().Be("short");
        JobSchedulerUiPresentation.ShortIdentity("1234567890123456", 8).Should().Be("12345678");
        JobSchedulerUiPresentation.ShortIdentity(null).Should().Be("—");
    }

    [Theory]
    [InlineData("owner.namespace.job", "job")]
    [InlineData("job", "job")]
    [InlineData("owner.", "")]
    public void GetJobKeyLabel_ShouldUseTheFinalKeySegment(string jobKey, string expected)
    {
        JobSchedulerUiPresentation.GetJobKeyLabel(jobKey).Should().Be(expected);
    }
}
