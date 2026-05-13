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
        var projector = CreateProjector();
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
        var projector = CreateProjector();
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

    [Fact]
    public void Project_WhenListItemKeySegmentsExist_ShouldEmitContiguousConfigurationIndices()
    {
        var projector = CreateProjector();
        var billingPath = new LogicalPath(
        [
            new PropertySegment("Services"),
            new ListItemKeySegment("billing"),
            new PropertySegment("Name")
        ]);
        var authPath = new LogicalPath(
        [
            new PropertySegment("Services"),
            new ListItemKeySegment("auth"),
            new PropertySegment("Name")
        ]);

        var projected = projector.Project(
            [TestConfigurationFactory.Definition()],
            [
                Merged(TestConfigurationFactory.Override(billingPath, json: "\"Billing\"")),
                Merged(TestConfigurationFactory.Override(authPath, json: "\"Auth\""))
            ]);

        projected.Should().BeEquivalentTo(
        [
            new ProjectedConfigurationKey { Key = "Test:App:Services:1:Name", Value = "Billing" },
            new ProjectedConfigurationKey { Key = "Test:App:Services:0:Name", Value = "Auth" }
        ]);
    }

    [Fact]
    public void Project_WhenWholeListSnapshotExists_ShouldResolveListItemKeysFromSnapshotItems()
    {
        var projector = CreateProjector();
        var listSnapshot = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("Services"),
            json: """[{"Name":"main","ConnectionString":"Server=main"},{"Name":"logs","ConnectionString":"Server=logs"}]""",
            granularity: ConfigurationOverrideGranularity.Container);
        var leaf = TestConfigurationFactory.Override(
            TestConfigurationFactory.ServiceItemPath("main").Append(new PropertySegment("Nested")).Append(new PropertySegment("Enabled")),
            json: "true");

        var projected = projector.Project(
            [TestConfigurationFactory.Definition()],
            [Merged(listSnapshot), Merged(leaf)]);

        projected.Should().ContainEquivalentOf(new ProjectedConfigurationKey
        {
            Key = "Test:App:Services:0:Nested:Enabled",
            Value = "true"
        });
    }

    [Fact]
    public void Project_WhenDictionaryContainerContainsStableListItems_ShouldResolveNestedListItemKeys()
    {
        var projector = CreateProjector();
        var dictionarySnapshot = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("ServiceMap"),
            json: """{"billing":{"ConnectedDbs":[{"Name":"main","ConnectionString":"Server=main"}]}}""",
            granularity: ConfigurationOverrideGranularity.Container);
        var leaf = TestConfigurationFactory.Override(
            TestConfigurationFactory.ConnectedDbPath("billing", "main").Append(new PropertySegment("ConnectionString")),
            json: "\"Server=override\"");

        var projected = projector.Project(
            [TestConfigurationFactory.Definition()],
            [Merged(dictionarySnapshot), Merged(leaf)]);

        projected.Should().ContainEquivalentOf(new ProjectedConfigurationKey
        {
            Key = "Test:App:ServiceMap:billing:ConnectedDbs:0:ConnectionString",
            Value = "Server=override"
        });
    }

    [Fact]
    public void Project_WhenHigherPriorityLeafOverlapsLowerPriorityContainer_ShouldEmitHigherPriorityFlatValue()
    {
        var projector = CreateProjector();
        var listSnapshot = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("Services"),
            json: """[{"Name":"main","ConnectionString":"Server=container"}]""",
            granularity: ConfigurationOverrideGranularity.Container);
        var leaf = TestConfigurationFactory.Override(
            TestConfigurationFactory.ServiceItemPath("main").Append(new PropertySegment("ConnectionString")),
            sourceKey: "memory:high",
            json: "\"Server=leaf\"");

        var projected = projector.Project(
            [TestConfigurationFactory.Definition()],
            [
                Merged(listSnapshot, sourcePriority: 100),
                Merged(leaf, sourcePriority: 200)
            ]);

        projected.Should().ContainSingle(item => item.Key == "Test:App:Services:0:ConnectionString")
            .Which.Value.Should().Be("Server=leaf");
    }

    private static ConfigurationProjector CreateProjector()
    {
        return new ConfigurationProjector(
            new ConfigurationPathProjector(),
            new ConfigurationStoredValueCodec(),
            new PassThroughSensitiveValueProtector());
    }

    private static MergedNodeValue Merged(ConfigurationValueOverride value, int sourcePriority = 100)
    {
        return new MergedNodeValue
        {
            DefinitionKey = value.DefinitionKey,
            LogicalPath = value.LogicalPath,
            Override = value,
            SourcePriority = sourcePriority
        };
    }
}
