using System.Text.Json;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

/// <summary>
/// Builds source-aware validation reports for the current runtime configuration.
/// </summary>
internal sealed class ConfigurationRuntimeValidationService(
    IConfigurationDefinitionRegistry definitionRegistry,
    ConfigurationEffectiveValueSeedFactory seedFactory,
    ConfigurationValueValidationEngine validationEngine,
    IConfigurationSourceInspector sourceInspector)
    : IConfigurationRuntimeValidationService
{
    private static readonly ConfigurationValueValidationOptions RUNTIME_OPTIONS = new();

    /// <summary>
    /// Builds a validation report for every local configuration definition.
    /// </summary>
    /// <returns>The validation report.</returns>
    public ConfigurationValidationReport GetReport()
    {
        var issues = definitionRegistry.GetAll()
            .SelectMany(GetIssues)
            .ToArray();

        return new ConfigurationValidationReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Issues = issues
        };
    }

    /// <summary>
    /// Builds a validation report for one local configuration definition.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <returns>The validation report.</returns>
    public ConfigurationValidationReport GetReport(string definitionKey)
    {
        var definition = definitionRegistry.GetRequired(definitionKey);
        return new ConfigurationValidationReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Issues = GetIssues(definition).ToArray()
        };
    }

    private IEnumerable<ConfigurationRuntimeValidationIssue> GetIssues(ConfigurationDefinition definition)
    {
        var json = seedFactory.CreateRuntimeJson(definition.Root, definition.SectionPath);
        using var document = JsonDocument.Parse(json);
        var issues = validationEngine.Validate(definition.Root, LogicalPath.Root, document.RootElement, RUNTIME_OPTIONS);
        foreach (var issue in issues)
        {
            yield return BuildRuntimeIssue(definition, issue);
        }
    }

    private ConfigurationRuntimeValidationIssue BuildRuntimeIssue(
        ConfigurationDefinition definition,
        ConfigurationValueValidationIssue issue)
    {
        var isSensitive = ConfigurationSchemaNavigator.IsSensitivePath(
            definition.Root,
            issue.LogicalPath);
        var sourceChain = sourceInspector.GetSourceChain(definition, issue.LogicalPath);
        var effectiveSource = sourceChain.Values.FirstOrDefault(value => value.IsEffective)?.Source;
        return new ConfigurationRuntimeValidationIssue
        {
            DefinitionKey = definition.DefinitionKey,
            DefinitionDisplayName = definition.DisplayName,
            DefinitionCategory = definition.Category,
            LogicalPath = issue.LogicalPath,
            NodeDisplayName = GetNodeLabel(definition, issue.Node),
            ConfigurationPath = sourceChain.ConfigurationPath,
            Problem = issue.Message,
            EffectiveDisplayValue = isSensitive ? null : issue.DisplayValue,
            IsMissing = issue.IsMissing,
            IsSensitive = isSensitive,
            EffectiveSource = effectiveSource,
            SourceChain = sourceChain,
            ValidationRules = issue.ValidationRules
        };
    }

    private static string GetNodeLabel(ConfigurationDefinition definition, ConfigurationNodeDefinition node)
    {
        if (node.RelativePath.Depth == 0)
        {
            return definition.DisplayName;
        }

        return string.IsNullOrWhiteSpace(node.DisplayName) ? node.Name : node.DisplayName;
    }
}
