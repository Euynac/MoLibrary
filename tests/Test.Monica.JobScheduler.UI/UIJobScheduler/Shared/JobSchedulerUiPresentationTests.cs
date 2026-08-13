using AwesomeAssertions;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;
using Monica.Testing.Localization;
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
    [InlineData(JobExecutionState.Cancelled, Color.Secondary)]
    [InlineData(JobExecutionState.Skipped, Color.Warning)]
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

    [Theory]
    [InlineData("0 */5 * * * *", "Catalog:Cron:EveryMinutes")]
    [InlineData("0 * * * *", "Catalog:Cron:EveryHour")]
    [InlineData("30 2 * * *", "Catalog:Cron:EveryDayAt")]
    [InlineData("0 0 * * MON", "Catalog:Cron:Custom")]
    public void DescribeCron_ShouldGiveDeterministicLocalizedDescriptions(string expression, string expectedKey)
    {
        var localizer = new EchoStringLocalizer<JobSchedulerResource>();

        JobSchedulerUiPresentation.DescribeCron(expression, localizer).Should().StartWith(expectedKey);
    }
}
