using Monica.Configuration.Models;

namespace Monica.Configuration.UI.State;

/// <summary>
/// Represents one invalid UI edit that cannot be converted into a persisted configuration mutation.
/// </summary>
public sealed record ConfigurationValidationIssue
{
    /// <summary>
    /// Gets the target definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the display name of the target definition.
    /// </summary>
    public required string DefinitionDisplayName { get; init; }

    /// <summary>
    /// Gets the target logical path.
    /// </summary>
    public required LogicalPath LogicalPath { get; init; }

    /// <summary>
    /// Gets the target node display name.
    /// </summary>
    public required string NodeDisplayName { get; init; }

    /// <summary>
    /// Gets the display-safe invalid value.
    /// </summary>
    public string? InvalidDisplayValue { get; init; }

    /// <summary>
    /// Gets the validation error shown to the operator.
    /// </summary>
    public required string ValidationError { get; init; }

    /// <summary>
    /// Gets whether the node contains sensitive data.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Gets the validation rules that explain why the value is constrained.
    /// </summary>
    public IReadOnlyList<ConfigurationValidationRule> ValidationRules { get; init; } = [];
}
