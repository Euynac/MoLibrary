using AwesomeAssertions;
using Monica.Configuration.Models;
using Monica.Configuration.Services;
using Xunit;

namespace Test.Monica.Configuration.Services;

public sealed class ConfigurationDefinitionRegistryTests
{
    [Fact]
    public void RegisterRange_WhenSourceEnumerationFails_ShouldPreserveThePreviousSnapshot()
    {
        var registry = new ConfigurationDefinitionRegistry();
        var existing = TestConfigurationFactory.Definition();
        registry.Register(existing);

        var act = () => registry.RegisterRange(EnumerateThenFail());

        act.Should().Throw<InvalidOperationException>().WithMessage("Analysis failed.");
        registry.GetAll().Should().ContainSingle().Which.Should().BeSameAs(existing);
    }

    private static IEnumerable<ConfigurationDefinition> EnumerateThenFail()
    {
        yield return TestConfigurationFactory.Definition() with
        {
            DefinitionKey = "Test.Pending"
        };
        throw new InvalidOperationException("Analysis failed.");
    }
}
