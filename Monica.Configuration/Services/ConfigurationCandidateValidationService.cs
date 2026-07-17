using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

/// <summary>
/// Maps mutation-profile validation results into the public candidate-validation contract.
/// </summary>
internal sealed class ConfigurationCandidateValidationService(
    ConfigurationValidationCoordinator validationCoordinator) : IConfigurationCandidateValidationService
{
    /// <summary>
    /// Validates a complete candidate value for one definition scope.
    /// </summary>
    /// <param name="definition">The active configuration definition.</param>
    /// <param name="scopePath">The logical path whose complete value is represented by <paramref name="json"/>.</param>
    /// <param name="json">The normalized candidate JSON value.</param>
    /// <returns>All mutation-time schema validation issues found in the candidate value.</returns>
    public ConfigurationCandidateValidationReport Validate(
        ConfigurationDefinition definition,
        LogicalPath scopePath,
        string json)
    {
        var issues = validationCoordinator.ValidateValue(definition, scopePath, json)
            .Select(issue => ToCandidateIssue(definition, issue))
            .ToArray();

        return new ConfigurationCandidateValidationReport
        {
            DefinitionKey = definition.DefinitionKey,
            DefinitionDisplayName = definition.DisplayName,
            ScopePath = scopePath,
            Issues = issues
        };
    }

    private static ConfigurationCandidateValidationIssue ToCandidateIssue(
        ConfigurationDefinition definition,
        ConfigurationValueValidationIssue issue)
    {
        var isSensitive = ConfigurationSchemaNavigator.IsSensitivePath(
            definition.Root,
            issue.LogicalPath);

        return new ConfigurationCandidateValidationIssue
        {
            DefinitionKey = definition.DefinitionKey,
            DefinitionDisplayName = definition.DisplayName,
            LogicalPath = issue.LogicalPath,
            NodeDisplayName = GetNodeLabel(definition, issue.Node),
            Problem = issue.Message,
            CandidateDisplayValue = isSensitive ? null : issue.DisplayValue,
            IsMissing = issue.IsMissing,
            IsSensitive = isSensitive,
            ValidationRules = issue.ValidationRules
        };
    }

    private static string GetNodeLabel(
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition node)
    {
        if (node.RelativePath.Depth == 0)
        {
            return definition.DisplayName;
        }

        return string.IsNullOrWhiteSpace(node.DisplayName) ? node.Name : node.DisplayName;
    }
}
