using AwesomeAssertions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;
using Xunit;

namespace Test.Monica.Configuration.Services.Support;

public class ConfigurationOverrideNormalizerTests
{
    [Fact]
    public void Normalize_WhenAncestorContainerSnapshotExists_ShouldDropDescendantLeafOverride()
    {
        var normalizer = new ConfigurationOverrideNormalizer();
        var container = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("Services"),
            json: """{"Name":"billing"}""",
            granularity: ConfigurationOverrideGranularity.Container);
        var descendant = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("Services", "Name"),
            json: "\"payments\"");

        var normalized = normalizer.Normalize([descendant, container]);

        normalized.Should().Equal(container);
    }

    [Fact]
    public void Normalize_WhenContainerSnapshotIsAddedAfterDescendant_ShouldRemoveExistingDescendantLeafOverride()
    {
        var normalizer = new ConfigurationOverrideNormalizer();
        var descendant = TestConfigurationFactory.Override(LogicalPath.FromProperties("Services", "Name"));
        var container = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("Services"),
            json: """{"Name":"billing"}""",
            granularity: ConfigurationOverrideGranularity.Container);

        var normalized = normalizer.Normalize([descendant, container]);

        normalized.Should().Equal(container);
    }

    [Fact]
    public void Normalize_WhenPathsAreSiblings_ShouldKeepBothOverrides()
    {
        var normalizer = new ConfigurationOverrideNormalizer();
        var first = TestConfigurationFactory.Override(LogicalPath.FromProperties("Services", "Name"));
        var second = TestConfigurationFactory.Override(LogicalPath.FromProperties("WorkerId"));

        var normalized = normalizer.Normalize([second, first]);

        normalized.Should().BeEquivalentTo([first, second]);
    }

    [Fact]
    public void Normalize_WhenOverlapsComeFromDifferentSources_ShouldKeepBothOverrides()
    {
        var normalizer = new ConfigurationOverrideNormalizer();
        var container = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("Services"),
            sourceKey: "memory:a",
            json: """{"Name":"billing"}""",
            granularity: ConfigurationOverrideGranularity.Container);
        var descendant = TestConfigurationFactory.Override(
            LogicalPath.FromProperties("Services", "Name"),
            sourceKey: "memory:b",
            json: "\"payments\"");

        var normalized = normalizer.Normalize([container, descendant]);

        normalized.Should().BeEquivalentTo([container, descendant]);
    }
}
