using AwesomeAssertions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;
using MudBlazor;
using Test.Monica.UI;
using Xunit;

namespace Test.Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;

public class JobStateColorResolverTests
{
    [Theory]
    [InlineData(JobState.Succeeded, Color.Success)]
    [InlineData(JobState.Failed, Color.Error)]
    [InlineData(JobState.Processing, Color.Primary)]
    public void GetStateColor_WhenKnownStateIsProvided_ShouldReturnExpectedMudColor(JobState state, Color expectedColor)
    {
        var resolver = new JobStateColorResolver(new TestThemeState());

        var result = resolver.GetStateColor(state);

        result.Should().Be(expectedColor);
    }

    [Fact]
    public void GetStateColorHex_WhenSkippedOrFailedStateIsProvided_ShouldReturnDeterministicHexValues()
    {
        var resolver = new JobStateColorResolver(new TestThemeState());

        resolver.GetStateColorHex(JobState.Skipped).Should().Be(Colors.Gray.Lighten1);
        resolver.GetStateColorHex(JobState.Failed).Should().Be("#d32f2f");
    }
}
