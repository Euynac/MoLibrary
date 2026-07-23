using AwesomeAssertions;
using Monica.UI.Shell.Support;
using Xunit;

namespace Test.Monica.UI.Shell.Support;

public sealed class NavigationRouteMatcherTests
{
    [Theory]
    [InlineData("gacha-pool", "gacha-pool")]
    [InlineData("GACHA-POOL", "/gacha-pool/")]
    [InlineData("gacha-pool/history", "gacha-pool")]
    [InlineData("gacha-pool?tab=overview", "gacha-pool")]
    [InlineData("gacha-pool#details", "gacha-pool")]
    [InlineData("", "")]
    public void IsActive_WhenDestinationOwnsCurrentRoute_ShouldReturnTrue(
        string currentRoute,
        string destination)
    {
        NavigationRouteMatcher.IsActive(currentRoute, destination).Should().BeTrue();
    }

    [Theory]
    [InlineData("gacha-pool-preview", "gacha-pool")]
    [InlineData("other/gacha-pool", "gacha-pool")]
    [InlineData("", "gacha-pool")]
    [InlineData("gacha-pool", "")]
    public void IsActive_WhenDestinationDoesNotOwnCurrentRoute_ShouldReturnFalse(
        string currentRoute,
        string destination)
    {
        NavigationRouteMatcher.IsActive(currentRoute, destination).Should().BeFalse();
    }
}
