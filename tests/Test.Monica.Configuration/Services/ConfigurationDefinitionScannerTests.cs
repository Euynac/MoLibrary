using System.ComponentModel.DataAnnotations;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Monica.Configuration.Annotations;
using Monica.Configuration.Models;
using Monica.Configuration.Services;
using Monica.Configuration.Services.Support;
using Xunit;

namespace Test.Monica.Configuration.Services;

public class ConfigurationDefinitionScannerTests
{
    [Fact]
    public void Scan_WhenTypeHasScalarProperties_ShouldBuildObjectRootWithScalarChildren()
    {
        var scanner = CreateScanner();

        var definition = scanner.Scan(typeof(SampleOptions));

        definition.DefinitionKey.Should().Be("test.sample");
        definition.SectionPath.Should().Be("Sample:App");
        definition.DisplayName.Should().Be("Sample Options");
        definition.Root.NodeKind.Should().Be(ConfigurationNodeKind.Object);
        definition.Root.Children.Select(x => x.Name).Should().Contain(["WorkerId", "Endpoint", "ApiKey"]);
        definition.Root.Children.Single(x => x.Name == "WorkerId").ValueKind.Should().Be(ConfigurationValueKind.Integer);
    }

    [Fact]
    public void Scan_WhenPropertyHasConfigurationKeyNameAttribute_ShouldUseConfiguredPropertyName()
    {
        var scanner = CreateScanner();

        var definition = scanner.Scan(typeof(SampleOptions));

        var endpoint = definition.Root.Children.Single(x => x.Name == "Endpoint");
        endpoint.RelativePath.Should().Be(LogicalPath.FromProperties("Endpoint"));
        endpoint.ConfigurationPath.Should().Be("Sample:App:Endpoint");
    }

    [Fact]
    public void Scan_WhenPropertyHasOptionSettingAttribute_ShouldApplyMetadata()
    {
        var scanner = CreateScanner();

        var definition = scanner.Scan(typeof(SampleOptions));

        var apiKey = definition.Root.Children.Single(x => x.Name == "ApiKey");
        apiKey.DisplayName.Should().Be("API key");
        apiKey.Description.Should().Be("Credential used by the sample integration.");
        apiKey.IsSensitive.Should().BeTrue();
        apiKey.ReloadBehavior.Should().Be(ConfigurationReloadBehavior.RequiresRestart);
        apiKey.NodeKey.Should().Be("sample.apiKey");
    }

    [Fact]
    public void Scan_WhenPropertiesUseDataAnnotations_ShouldMapThemToValidationRules()
    {
        var scanner = CreateScanner();

        var definition = scanner.Scan(typeof(SampleOptions));

        var worker = definition.Root.Children.Single(x => x.Name == "WorkerId");
        var endpoint = definition.Root.Children.Single(x => x.Name == "Endpoint");
        var apiKey = definition.Root.Children.Single(x => x.Name == "ApiKey");

        worker.ValidationRules.Should().ContainSingle()
            .Which.Should().Be(new RangeRule(0, 1023));
        endpoint.ValidationRules.OfType<RequiredRule>().Should().ContainSingle();
        endpoint.ValidationRules.OfType<RegexRule>().Should().ContainSingle()
            .Which.Pattern.Should().Be("^https://");
        apiKey.ValidationRules.OfType<MaxLengthRule>().Should().ContainSingle()
            .Which.Max.Should().Be(64);
    }

    private static ConfigurationDefinitionScanner CreateScanner()
    {
        return new ConfigurationDefinitionScanner(new ConfigurationSchemaHasher());
    }

    [Configuration("Sample:App", DefinitionKey = "test.sample", DisplayName = "Sample Options")]
    private sealed class SampleOptions
    {
        [Range(0, 1023)]
        public int WorkerId { get; set; }

        [Required]
        [RegularExpression("^https://")]
        [ConfigurationKeyName("Endpoint")]
        public string? ServiceUrl { get; set; }

        [MaxLength(64)]
        [OptionSetting("API key",
            Description = "Credential used by the sample integration.",
            IsSensitive = true,
            NodeKey = "sample.apiKey",
            ReloadBehavior = ConfigurationReloadBehavior.RequiresRestart)]
        public string? ApiKey { get; set; }
    }
}
