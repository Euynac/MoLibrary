using AwesomeAssertions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;
using Xunit;

namespace Test.Monica.Configuration.Services.Support;

public class ConfigurationSchemaHasherTests
{
    [Fact]
    public void ComputeHash_WhenDefinitionIsSame_ShouldReturnStableHash()
    {
        var hasher = new ConfigurationSchemaHasher();
        var root = TestConfigurationFactory.RootNode();

        var first = hasher.ComputeHash(TestConfigurationFactory.DefinitionKey, "Test:App", root);
        var second = hasher.ComputeHash(TestConfigurationFactory.DefinitionKey, "Test:App", root);

        first.Should().Be(second);
        first.Should().StartWith("sha256:");
    }

    [Fact]
    public void ComputeHash_WhenNodeKindChanges_ShouldReturnDifferentHash()
    {
        var hasher = new ConfigurationSchemaHasher();
        var scalarRoot = TestConfigurationFactory.RootNode();
        var objectRoot = scalarRoot with
        {
            Children =
            [
                TestConfigurationFactory.ObjectNode(
                    "WorkerId",
                    [
                        TestConfigurationFactory.ScalarNode("Nested", typeof(string), ConfigurationValueKind.String)
                    ])
            ]
        };

        var scalarHash = hasher.ComputeHash(TestConfigurationFactory.DefinitionKey, "Test:App", scalarRoot);
        var objectHash = hasher.ComputeHash(TestConfigurationFactory.DefinitionKey, "Test:App", objectRoot);

        objectHash.Should().NotBe(scalarHash);
    }
}
