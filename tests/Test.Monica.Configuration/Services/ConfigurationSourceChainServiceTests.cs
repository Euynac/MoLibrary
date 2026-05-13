using AwesomeAssertions;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Services;
using Monica.Configuration.Services.Support;
using Test.Monica.Configuration.Services.Support;
using Xunit;

namespace Test.Monica.Configuration.Services;

public class ConfigurationSourceChainServiceTests
{
    [Fact]
    public async Task GetSourceChainAsync_WhenTargetNodeIsSensitive_ShouldRedactDisplayValue()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var registry = new ConfigurationDefinitionRegistry();
        var path = LogicalPath.FromProperties("ApiKey");
        registry.Register(TestConfigurationFactory.Definition() with
        {
            Root = TestConfigurationFactory.RootNode() with
            {
                Children =
                [
                    TestConfigurationFactory.ScalarNode(
                        "ApiKey",
                        typeof(string),
                        ConfigurationValueKind.String,
                        isSensitive: true)
                ]
            }
        });
        var source = new StaticValueSource(TestConfigurationFactory.Override(path, json: "\"secret\""));
        var service = new ConfigurationSourceChainService(
            registry,
            [source],
            new ConfigurationStoredValueCodec(),
            new PassThroughSensitiveValueProtector());

        var chain = await service.GetSourceChainAsync(TestConfigurationFactory.DefinitionKey, path, cancellationToken);

        chain.Sources.Should().ContainSingle();
        chain.Sources[0].IsSensitive.Should().BeTrue();
        chain.Sources[0].DisplayValue.Should().BeNull();
    }

    private sealed class StaticValueSource(ConfigurationValueOverride value) : IConfigurationValueSource
    {
        public ConfigurationSourceDescriptor Descriptor { get; } = TestConfigurationFactory.Source("memory:test", 100);

        public Task<IReadOnlyList<ConfigurationValueOverride>> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ConfigurationValueOverride>>([value]);
        }

        public Task<ConfigurationValueOverride?> GetAsync(
            string definitionKey,
            LogicalPath logicalPath,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ConfigurationValueOverride?>(value);
        }

        public Task<ConfigurationMutationResult> MutateAsync(
            ConfigurationSourceMutation mutation,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
