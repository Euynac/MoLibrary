using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Projection;
using Monica.Configuration.Services;
using Monica.Configuration.Services.Support;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Configuration.Services;

public sealed class ConfigurationSourceInspectorTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"monica-source-inspector-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task GetSourceInventories_WhenJsonProvidersSharePhysicalFile_ShouldKeepProviderInstancesDistinct()
    {
        Directory.CreateDirectory(_rootDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(_rootDirectory, "appsettings.json"),
            """
            {
              "Test": {
                "App": {
                  "WorkerId": 7
                }
              }
            }
            """,
            TestContext.Current.CancellationToken);
        var configuration = new ConfigurationBuilder()
            .SetBasePath(_rootDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();
        var inspector = CreateInspector(configuration);

        var sources = inspector.GetSources();
        var inventories = inspector.GetSourceInventories();

        sources.Should().HaveCount(2);
        sources.Select(source => source.SourceKey).Should().OnlyHaveUniqueItems();
        inventories.Should().HaveCount(2);
        inventories.Select(inventory => inventory.Source.SourceKey).Should().OnlyHaveUniqueItems();
        inventories.Select(inventory => inventory.Source.PriorityIndex).Should().Equal(1, 0);
        inventories.Should().AllSatisfy(inventory =>
        {
            inventory.SuppliedValueCount.Should().Be(1);
            inventory.Items.Should().ContainSingle(item => item.ConfigurationPath == "Test:App:WorkerId");
        });
        inventories.Single(inventory => inventory.Source.PriorityIndex == 1).EffectiveValueCount.Should().Be(1);
        inventories.Single(inventory => inventory.Source.PriorityIndex == 0).EffectiveValueCount.Should().Be(0);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private static ConfigurationSourceInspector CreateInspector(IConfiguration configuration)
    {
        var runtimeContext = new ConfigurationRuntimeContext();
        runtimeContext.Capture(configuration);
        var registry = new ConfigurationDefinitionRegistry();
        registry.Register(TestConfigurationFactory.Definition());

        return new ConfigurationSourceInspector(
            runtimeContext,
            registry,
            new ConfigurationPathProjector(),
            Options.Create(new ModuleConfigurationOption
            {
                IncludeUnmanagedSourceInventoryItems = false
            }));
    }
}
