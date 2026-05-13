using AwesomeAssertions;
using Monica.Configuration.Models;
using Monica.Configuration.Models.Internal;
using Monica.Configuration.Services.Support;
using Xunit;

namespace Test.Monica.Configuration.Services.Support;

public class ConfigurationMergeEngineTests
{
    [Fact]
    public void Merge_WhenSamePathExistsInMultipleSources_ShouldUseHigherPrioritySource()
    {
        var engine = new ConfigurationMergeEngine();
        var low = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("WorkerId"),
            sourceKey: "memory:low",
            json: "1");
        var high = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("WorkerId"),
            sourceKey: "memory:high",
            json: "2");

        var merged = engine.Merge(
        [
            SourceSet("memory:low", 100, [low]),
            SourceSet("memory:high", 200, [high])
        ]);

        merged.Should().ContainSingle();
        merged[0].Override.Should().BeSameAs(high);
    }

    [Fact]
    public void Merge_WhenHigherPrioritySourceRemovesSubtree_ShouldPruneLowerPriorityDescendants()
    {
        var engine = new ConfigurationMergeEngine();
        var tombstone = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("Services"),
            sourceKey: "memory:high",
            state: ConfigurationValueState.RemovedSubtree,
            granularity: ConfigurationOverrideGranularity.Container);
        var lowerDescendant = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("Services", "Name"),
            sourceKey: "memory:low");

        var merged = engine.Merge(
        [
            SourceSet("memory:low", 100, [lowerDescendant]),
            SourceSet("memory:high", 200, [tombstone])
        ]);

        merged.Should().BeEmpty();
    }

    [Fact]
    public void Merge_WhenHigherPrioritySourceRemovesOverride_ShouldRevealLowerPrioritySamePath()
    {
        var engine = new ConfigurationMergeEngine();
        var removed = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("WorkerId"),
            sourceKey: "memory:high",
            state: ConfigurationValueState.RemovedOverride);
        var lower = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("WorkerId"),
            sourceKey: "memory:low",
            json: "1");

        var merged = engine.Merge(
        [
            SourceSet("memory:low", 100, [lower]),
            SourceSet("memory:high", 200, [removed])
        ]);

        merged.Should().ContainSingle();
        merged[0].Override.Should().BeSameAs(lower);
    }

    [Fact]
    public void Merge_WhenHigherPriorityContainerSnapshotIsActive_ShouldSuppressLowerPriorityDescendants()
    {
        var engine = new ConfigurationMergeEngine();
        var container = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("Services"),
            sourceKey: "memory:high",
            json: """{"Name":"billing"}""",
            granularity: ConfigurationOverrideGranularity.Container);
        var lowerDescendant = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("Services", "Name"),
            sourceKey: "memory:low",
            json: "\"payments\"");

        var merged = engine.Merge(
        [
            SourceSet("memory:low", 100, [lowerDescendant]),
            SourceSet("memory:high", 200, [container])
        ]);

        merged.Should().ContainSingle();
        merged[0].Override.Should().BeSameAs(container);
    }

    private static NormalizedOverrideSet SourceSet(
        string sourceKey,
        int priority,
        IReadOnlyList<ConfigurationValueOverride> overrides)
    {
        return new NormalizedOverrideSet
        {
            Source = TestConfigurationFactory.Source(sourceKey, priority),
            Overrides = overrides
        };
    }
}
