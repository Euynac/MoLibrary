using AwesomeAssertions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.AI.Modules;

public sealed class ModuleAIEndpointsTests
{
    [Theory]
    [InlineData("ai", "/ai")]
    [InlineData(" /custom/ ", "/custom")]
    [InlineData("/", "/")]
    public void ValidateOptions_ShouldNormalizeThePublicRoutePrefix(
        string configuredPrefix,
        string expectedPrefix)
    {
        var module = new ModuleAIEndpoints();
        var options = new ModuleAIEndpointsOption
        {
            RoutePrefix = configuredPrefix
        };

        module.ValidateOptions(options, profileName: null);

        options.RoutePrefix.Should().Be(expectedPrefix);
    }

    [Fact]
    public void ValidateOptions_WhenRoutePrefixIsBlank_ShouldRejectTheConfiguration()
    {
        var module = new ModuleAIEndpoints();
        var options = new ModuleAIEndpointsOption
        {
            RoutePrefix = " "
        };

        Action validate = () => module.ValidateOptions(options, profileName: null);

        validate.Should().Throw<ArgumentException>();
    }
}
