using AwesomeAssertions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Providers.Memory;
using Xunit;

namespace Test.Monica.Configuration.Providers.Memory;

public class MemoryConfigurationValueSourceTests
{
    [Fact]
    public async Task MutateAsync_WhenSettingScalarValue_ShouldStoreOverrideAndReturnItFromLoad()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var source = new MemoryConfigurationValueSource();
        var mutation = Mutation(
            LogicalPath.FromProperties("WorkerId"),
            ConfigurationStoredValue.Plain("12"));

        await source.MutateAsync(mutation, cancellationToken);

        var values = await source.LoadAsync(cancellationToken);
        values.Should().ContainSingle();
        values[0].LogicalPath.Should().Be(LogicalPath.FromProperties("WorkerId"));
        values[0].Value.PlainJson.Should().Be("12");
        values[0].Version.Should().Be(1);
    }

    [Fact]
    public async Task MutateAsync_WhenSameScalarIsSetTwice_ShouldOverwriteValueAndIncrementVersion()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var source = new MemoryConfigurationValueSource();
        var path = LogicalPath.FromProperties("WorkerId");

        await source.MutateAsync(Mutation(path, ConfigurationStoredValue.Plain("12")), cancellationToken);
        await source.MutateAsync(Mutation(path, ConfigurationStoredValue.Plain("13")), cancellationToken);

        var values = await source.LoadAsync(cancellationToken);
        values.Should().ContainSingle();
        values[0].Value.PlainJson.Should().Be("13");
        values[0].Version.Should().Be(2);
    }

    [Fact]
    public async Task MutateAsync_WhenContainerSnapshotCoversTarget_ShouldPatchContainerInsteadOfAddingLeaf()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var source = new MemoryConfigurationValueSource();
        var containerPath = TestConfigurationFactory.ServiceItemPath("billing");
        var descendantPath = containerPath.Append(new PropertySegment("Name"));

        await source.MutateAsync(Mutation(
            containerPath,
            ConfigurationStoredValue.Plain("""{"Name":"billing","Nested":{"Enabled":false}}"""),
            ConfigurationMutationKind.Replace,
            ConfigurationOverrideGranularity.Container), cancellationToken);

        await source.MutateAsync(Mutation(
            descendantPath,
            ConfigurationStoredValue.Plain("\"payments\"")), cancellationToken);

        var values = await source.LoadAsync(cancellationToken);
        values.Should().ContainSingle();
        values[0].LogicalPath.Should().Be(containerPath);
        values[0].Granularity.Should().Be(ConfigurationOverrideGranularity.Container);
        values[0].Value.PlainJson.Should().Be("""{"Name":"payments","Nested":{"Enabled":false}}""");
        values[0].Version.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_WhenContainerSnapshotCoversTarget_ShouldReturnCoveringContainer()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var source = new MemoryConfigurationValueSource();
        var containerPath = TestConfigurationFactory.ServiceItemPath("billing");
        var descendantPath = containerPath.Append(new PropertySegment("Name"));

        await source.MutateAsync(Mutation(
            containerPath,
            ConfigurationStoredValue.Plain("""{"Name":"billing"}"""),
            ConfigurationMutationKind.Replace,
            ConfigurationOverrideGranularity.Container), cancellationToken);

        var value = await source.GetAsync(TestConfigurationFactory.DefinitionKey, descendantPath, cancellationToken);

        value.Should().NotBeNull();
        value!.LogicalPath.Should().Be(containerPath);
        value.Granularity.Should().Be(ConfigurationOverrideGranularity.Container);
    }

    [Fact]
    public async Task MutateAsync_WhenWholeListContainerCoversStableItemTarget_ShouldPatchListSnapshot()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var source = new MemoryConfigurationValueSource();
        var containerPath = LogicalPath.FromProperties("Services");
        var descendantPath = TestConfigurationFactory.ServiceItemPath("main").Append(new PropertySegment("ConnectionString"));

        await source.MutateAsync(Mutation(
            containerPath,
            ConfigurationStoredValue.Plain("""[{"Name":"main","ConnectionString":"old"},{"Name":"logs","ConnectionString":"old"}]"""),
            ConfigurationMutationKind.Replace,
            ConfigurationOverrideGranularity.Container), cancellationToken);

        await source.MutateAsync(Mutation(
            descendantPath,
            ConfigurationStoredValue.Plain("\"new\"")), cancellationToken);

        var values = await source.LoadAsync(cancellationToken);
        values.Should().ContainSingle();
        values[0].LogicalPath.Should().Be(containerPath);
        values[0].Value.PlainJson.Should().Be(
            """[{"Name":"main","ConnectionString":"new"},{"Name":"logs","ConnectionString":"old"}]""");
    }

    [Fact]
    public async Task MutateAsync_WhenContainerReplaceFollowsLeaf_ShouldRemoveDescendantLeaf()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var source = new MemoryConfigurationValueSource();
        var containerPath = TestConfigurationFactory.ServiceMapPath("billing");
        var descendantPath = TestConfigurationFactory.ConnectedDbPath("billing", "main").Append(new PropertySegment("ConnectionString"));

        await source.MutateAsync(Mutation(
            descendantPath,
            ConfigurationStoredValue.Plain("\"leaf\"")), cancellationToken);

        await source.MutateAsync(Mutation(
            containerPath,
            ConfigurationStoredValue.Plain("""{"ConnectedDbs":[{"Name":"main","ConnectionString":"container"}]}"""),
            ConfigurationMutationKind.Replace,
            ConfigurationOverrideGranularity.Container), cancellationToken);

        var values = await source.LoadAsync(cancellationToken);
        values.Should().ContainSingle();
        values[0].LogicalPath.Should().Be(containerPath);
        values[0].Granularity.Should().Be(ConfigurationOverrideGranularity.Container);
        values[0].Value.PlainJson.Should().Be("""{"ConnectedDbs":[{"Name":"main","ConnectionString":"container"}]}""");
    }

    [Fact]
    public async Task MutateAsync_WhenDictionaryContainerCoversStableListItemTarget_ShouldPatchDictionarySnapshot()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var source = new MemoryConfigurationValueSource();
        var containerPath = LogicalPath.FromProperties("ServiceMap");
        var descendantPath = TestConfigurationFactory.ConnectedDbPath("billing", "main").Append(new PropertySegment("ConnectionString"));

        await source.MutateAsync(Mutation(
            containerPath,
            ConfigurationStoredValue.Plain("""{"billing":{"ConnectedDbs":[{"Name":"main","ConnectionString":"old"}]}}"""),
            ConfigurationMutationKind.Replace,
            ConfigurationOverrideGranularity.Container), cancellationToken);

        await source.MutateAsync(Mutation(
            descendantPath,
            ConfigurationStoredValue.Plain("\"new\"")), cancellationToken);

        var values = await source.LoadAsync(cancellationToken);
        values.Should().ContainSingle();
        values[0].LogicalPath.Should().Be(containerPath);
        values[0].Value.PlainJson.Should().Be(
            """{"billing":{"ConnectedDbs":[{"Name":"main","ConnectionString":"new"}]}}""");
    }

    [Fact]
    public async Task MutateAsync_WhenExpectedVersionDoesNotMatch_ShouldThrowConcurrencyConflict()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var source = new MemoryConfigurationValueSource();
        var path = LogicalPath.FromProperties("WorkerId");

        await source.MutateAsync(Mutation(path, ConfigurationStoredValue.Plain("12")), cancellationToken);

        var act = () => source.MutateAsync(Mutation(
            path,
            ConfigurationStoredValue.Plain("13"),
            expectedValueVersion: 7), cancellationToken);

        await act.Should().ThrowAsync<ConfigurationConcurrencyConflictException>();
    }

    private static ConfigurationSourceMutation Mutation(
        LogicalPath path,
        ConfigurationStoredValue value,
        ConfigurationMutationKind mutationKind = ConfigurationMutationKind.Set,
        ConfigurationOverrideGranularity granularity = ConfigurationOverrideGranularity.Scalar,
        long? expectedValueVersion = null)
    {
        return new ConfigurationSourceMutation
        {
            Request = new ConfigurationMutationRequest
            {
                DefinitionKey = TestConfigurationFactory.DefinitionKey,
                LogicalPath = path,
                MutationKind = mutationKind,
                Value = value,
                ExpectedValueVersion = expectedValueVersion,
                Context = new ConfigurationMutationContext
                {
                    ModifierId = "tester",
                    ModifierName = "Tester"
                }
            },
            Definition = TestConfigurationFactory.Definition(),
            TargetNode = TestConfigurationFactory.ScalarNode("WorkerId", typeof(int), ConfigurationValueKind.Integer),
            SourceKey = "memory:default",
            ConfigurationPath = $"Test:App:{string.Join(':', path.Segments.Select(segment => segment.Value))}",
            Granularity = granularity
        };
    }
}
