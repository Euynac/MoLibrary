using AwesomeAssertions;
using Monica.Configuration.Models;
using Monica.Configuration.Models.Internal;
using Monica.Configuration.Services.Support;
using Xunit;

namespace Test.Monica.Configuration.Services.Support;

public class ConfigurationProjectorTests
{
    [Fact]
    public void Project_WhenScalarValueExists_ShouldEmitSectionPrefixedConfigurationKey()
    {
        var projector = new ConfigurationProjector(new ConfigurationPathProjector(), new ConfigurationStoredValueCodec());
        var value = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("WorkerId"),
            json: "12");

        var projected = projector.Project(
            [TestConfigurationFactory.Definition()],
            [Merged(value)]);

        projected.Should().ContainSingle();
        projected[0].Key.Should().Be("Test:App:WorkerId");
        projected[0].Value.Should().Be("12");
    }

    [Fact]
    public void Project_WhenContainerSnapshotExists_ShouldFlattenJsonObjectTree()
    {
        var projector = new ConfigurationProjector(new ConfigurationPathProjector(), new ConfigurationStoredValueCodec());
        var value = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("Services"),
            json: """{"Name":"billing","Nested":{"Enabled":true}}""",
            granularity: ConfigurationOverrideGranularity.Container);

        var projected = projector.Project(
            [TestConfigurationFactory.Definition()],
            [Merged(value)]);

        projected.Should().BeEquivalentTo(
        [
            new ProjectedConfigurationKey { Key = "Test:App:Services:Name", Value = "billing" },
            new ProjectedConfigurationKey { Key = "Test:App:Services:Nested:Enabled", Value = "true" }
        ]);
    }

    private static MergedNodeValue Merged(ConfigurationValueOverride value)
    {
        return new MergedNodeValue
        {
            DefinitionKey = value.DefinitionKey,
            LogicalPath = value.LogicalPath,
            Override = value
        };
    }
}
