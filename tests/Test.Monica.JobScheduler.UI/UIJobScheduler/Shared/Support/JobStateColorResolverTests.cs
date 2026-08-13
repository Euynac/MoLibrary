using AwesomeAssertions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;
using MudBlazor;
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
        var resolver = new JobStateColorResolver();

        var result = resolver.GetStateColor(state);

        result.Should().Be(expectedColor);
    }

    [Fact]
    public void GetStateColorToken_WhenSkippedOrFailedStateIsProvided_ShouldReturnSemanticThemeTokens()
    {
        JobStateColorResolver.GetStateColorToken(JobState.Skipped).Should().Be("var(--mud-palette-text-disabled)");
        JobStateColorResolver.GetStateColorToken(JobState.Failed).Should().Be("var(--mud-palette-error)");
    }
}
