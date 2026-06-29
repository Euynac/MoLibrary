using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Configuration.Annotations;
using Monica.Configuration.Bootstrap;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Configuration.Binding;

public class MonicaConfigurationCollectionBindingTests
{
    [Fact]
    public void BootstrapBinding_WhenConfiguredListHasDefaults_ShouldReplaceDefaultItems()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Bootstrap:Items:0"] = "configured"
        });

        var options = configuration.GetMonicaBootstrapConfiguration<BootstrapListOptions>();

        options.Items.Should().Equal("configured");
    }

    [Fact]
    public void BootstrapBinding_WhenListSectionIsMissing_ShouldPreserveDefaultItems()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["MissingList:Name"] = "configured"
        });

        var options = configuration.GetMonicaBootstrapConfiguration<MissingListOptions>();

        options.Items.Should().Equal("default");
    }

    [Fact]
    public void BootstrapBinding_WhenNestedListHasDefaults_ShouldReplaceNestedDefaultItems()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Nested:Nested:Items:0"] = "configured"
        });

        var options = configuration.GetMonicaBootstrapConfiguration<NestedCollectionOptions>();

        options.Nested.Items.Should().Equal("configured");
    }

    [Fact]
    public void BootstrapBinding_WhenConfiguredDictionaryHasDefaults_ShouldReplaceDefaultEntries()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Dictionary:Map:configured"] = "value"
        });

        var options = configuration.GetMonicaBootstrapConfiguration<DictionaryOptions>();

        options.Map.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, string>("configured", "value"));
    }

    [Fact]
    public void BootstrapBinding_WhenCollectionUsesConfigurationKeyName_ShouldReplaceDefaultItems()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Alias:ConfiguredItems:0"] = "configured"
        });

        var options = configuration.GetMonicaBootstrapConfiguration<AliasOptions>();

        options.Items.Should().Equal("configured");
    }

    [Fact]
    public void RuntimeOptionsBinding_WhenConfiguredListHasDefaults_ShouldReplaceDefaultItems()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Runtime:Items:0"] = "configured"
        });
        var module = new ModuleConfiguration(new ModuleConfigurationOption());
        module.ConfigureBuilder(builder);
        module.ConfigureServices(builder.Services);
        module.IterateBusinessTypes([typeof(RuntimeOptions)]).ToArray();

        using var serviceProvider = builder.Services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<RuntimeOptions>>().Value;

        options.Items.Should().Equal("configured");
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Configuration("Bootstrap")]
    private sealed class BootstrapListOptions
    {
        public List<string> Items { get; set; } = ["default"];
    }

    [Configuration("MissingList")]
    private sealed class MissingListOptions
    {
        public string? Name { get; set; }

        public List<string> Items { get; set; } = ["default"];
    }

    [Configuration("Nested")]
    private sealed class NestedCollectionOptions
    {
        public NestedOptions Nested { get; set; } = new();
    }

    private sealed class NestedOptions
    {
        public List<string> Items { get; set; } = ["default"];
    }

    [Configuration("Dictionary")]
    private sealed class DictionaryOptions
    {
        public Dictionary<string, string> Map { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = "default-value"
        };
    }

    [Configuration("Alias")]
    private sealed class AliasOptions
    {
        [ConfigurationKeyName("ConfiguredItems")]
        public List<string> Items { get; set; } = ["default"];
    }

    [Configuration("Runtime")]
    private sealed class RuntimeOptions
    {
        public List<string> Items { get; set; } = ["default"];
    }
}
