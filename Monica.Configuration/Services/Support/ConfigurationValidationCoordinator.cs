using System.Text.Json;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Coordinates mutation-time validation.
/// </summary>
internal sealed class ConfigurationValidationCoordinator(ConfigurationValueValidationEngine validationEngine)
{
    private static readonly ConfigurationValueValidationOptions MUTATION_OPTIONS = new()
    {
        TreatNonNullableScalarsAsRequired = true
    };

    /// <summary>
    /// Validates a mutation against the current schema constraints.
    /// </summary>
    /// <param name="definition">The target definition.</param>
    /// <param name="request">The mutation request.</param>
    public void Validate(ConfigurationDefinition definition, ConfigurationMutationRequest request)
    {
        if (request.ExpectedSchemaVersion > 0 && request.ExpectedSchemaVersion != definition.SchemaVersion)
        {
            throw new ConfigurationSchemaMismatchException(
                $"Expected schema version {request.ExpectedSchemaVersion}, but definition '{definition.DefinitionKey}' is version {definition.SchemaVersion}.");
        }

        var target = ResolveTargetNode(definition, request.LogicalPath);
        var issues = request.MutationKind == ConfigurationMutationKind.Remove
            ? validationEngine.ValidateRemoval(target, request.LogicalPath, MUTATION_OPTIONS)
            : ValidateValue(target, request);

        if (issues.FirstOrDefault() is { } issue)
        {
            throw new ConfigurationValidationFailedException($"{issue.LogicalPath.ToCanonicalString()}: {issue.Message}");
        }
    }

    private IReadOnlyList<ConfigurationValueValidationIssue> ValidateValue(
        ConfigurationNodeDefinition target,
        ConfigurationMutationRequest request)
    {
        using var document = JsonDocument.Parse(request.Value.Json);
        return validationEngine.Validate(target, request.LogicalPath, document.RootElement, MUTATION_OPTIONS);
    }

    private static ConfigurationNodeDefinition ResolveTargetNode(
        ConfigurationDefinition definition,
        LogicalPath logicalPath)
    {
        var current = definition.Root;
        foreach (var segment in logicalPath.Segments)
        {
            current = segment switch
            {
                PropertySegment property => current.Children.FirstOrDefault(child =>
                    string.Equals(child.Name, property.Name, StringComparison.OrdinalIgnoreCase)),
                DictionaryKeySegment => current.DictionaryTemplate?.ValueTemplate,
                ListItemKeySegment or ListIndexSegment => current.ListTemplate?.ItemTemplate,
                _ => null
            };

            if (current is null)
            {
                throw new ConfigurationValidationFailedException(
                    $"{logicalPath.ToCanonicalString()}: Logical path '{logicalPath}' does not exist in definition '{definition.DefinitionKey}'.");
            }
        }

        return current;
    }
}
