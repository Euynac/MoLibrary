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
        TreatNonNullableScalarsAsRequired = true,
        RejectUnknownObjectProperties = true
    };

    // Captured historical values legitimately predate the current schema: properties may have been
    // removed from the definition since capture. Unknown properties never reach persistence planning
    // (the planner only walks schema-declared nodes), so they are tolerated here instead of rejecting
    // the rollback. Missing non-nullable scalars stay fatal because restoring would silently fall back
    // to a default the operator never reviewed.
    private static readonly ConfigurationValueValidationOptions CAPTURED_VALUE_OPTIONS = new()
    {
        TreatNonNullableScalarsAsRequired = true,
        RejectUnknownObjectProperties = false
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

        if (request.ExpectedSchemaHash is { Length: > 0 } expectedSchemaHash
            && !string.Equals(expectedSchemaHash, definition.SchemaHash, StringComparison.Ordinal))
        {
            throw new ConfigurationSchemaMismatchException(
                $"The reviewed schema fingerprint for definition '{definition.DefinitionKey}' no longer matches the active schema.");
        }

        var target = ResolveTargetNode(definition, request.LogicalPath);
        var issues = request.MutationKind == ConfigurationMutationKind.Remove
            ? validationEngine.ValidateRemoval(target, request.LogicalPath, MUTATION_OPTIONS)
            : ValidateValue(target, request.LogicalPath, request.Value.Json);

        if (issues.FirstOrDefault() is { } issue)
        {
            throw new ConfigurationValidationFailedException($"{issue.LogicalPath.ToCanonicalString()}: {issue.Message}");
        }
    }

    /// <summary>
    /// Validates a complete JSON value against the definition's current schema and returns every issue.
    /// </summary>
    /// <param name="definition">The current configuration definition.</param>
    /// <param name="json">The complete root JSON value.</param>
    /// <returns>All schema validation issues found in the value.</returns>
    public IReadOnlyList<ConfigurationValueValidationIssue> ValidateValue(
        ConfigurationDefinition definition,
        string json)
    {
        return ValidateValue(definition, LogicalPath.Root, json);
    }

    /// <summary>
    /// Validates a captured historical value against the definition's current schema using rollback tolerance.
    /// </summary>
    /// <param name="definition">The current configuration definition.</param>
    /// <param name="json">The captured complete root JSON value.</param>
    /// <returns>
    /// The remaining hard incompatibilities. Unknown object properties are ignored because persistence
    /// planning only writes schema-declared paths.
    /// </returns>
    public IReadOnlyList<ConfigurationValueValidationIssue> ValidateCapturedValue(
        ConfigurationDefinition definition,
        string json)
    {
        var target = ResolveTargetNode(definition, LogicalPath.Root);
        using var document = JsonDocument.Parse(json);
        return validationEngine.Validate(target, LogicalPath.Root, document.RootElement, CAPTURED_VALUE_OPTIONS);
    }

    /// <summary>
    /// Validates a complete JSON value for one definition scope and returns every issue.
    /// </summary>
    /// <param name="definition">The current configuration definition.</param>
    /// <param name="logicalPath">The logical path whose complete value is represented by <paramref name="json"/>.</param>
    /// <param name="json">The complete JSON value for the target scope.</param>
    /// <returns>All schema validation issues found in the value.</returns>
    public IReadOnlyList<ConfigurationValueValidationIssue> ValidateValue(
        ConfigurationDefinition definition,
        LogicalPath logicalPath,
        string json)
    {
        var target = ResolveTargetNode(definition, logicalPath);
        return ValidateValue(target, logicalPath, json);
    }

    private IReadOnlyList<ConfigurationValueValidationIssue> ValidateValue(
        ConfigurationNodeDefinition target,
        LogicalPath logicalPath,
        string json)
    {
        using var document = JsonDocument.Parse(json);
        return validationEngine.Validate(target, logicalPath, document.RootElement, MUTATION_OPTIONS);
    }

    private static ConfigurationNodeDefinition ResolveTargetNode(
        ConfigurationDefinition definition,
        LogicalPath logicalPath)
    {
        if (ConfigurationSchemaNavigator.ResolveNode(definition.Root, logicalPath) is { } target)
        {
            return target;
        }

        throw new ConfigurationValidationFailedException(
            $"{logicalPath.ToCanonicalString()}: Logical path '{logicalPath}' does not exist in definition '{definition.DefinitionKey}'.");
    }
}
