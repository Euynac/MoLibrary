namespace Monica.Configuration.Models;

/// <summary>
/// Describes schema validation results for an operator-provided candidate value.
/// </summary>
public sealed record ConfigurationCandidateValidationReport
{
    /// <summary>
    /// Gets the owning configuration definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the display name of the owning configuration definition.
    /// </summary>
    public required string DefinitionDisplayName { get; init; }

    /// <summary>
    /// Gets the logical path whose complete candidate value was validated.
    /// </summary>
    public required LogicalPath ScopePath { get; init; }

    /// <summary>
    /// Gets all validation issues found in the candidate value.
    /// </summary>
    public IReadOnlyList<ConfigurationCandidateValidationIssue> Issues { get; init; } = [];

    /// <summary>
    /// Gets whether the candidate value satisfies every mutation-time schema constraint.
    /// </summary>
    public bool IsValid => Issues.Count == 0;

    /// <summary>
    /// Gets the total number of validation issues.
    /// </summary>
    public int IssueCount => Issues.Count;
}

/// <summary>
/// Describes one mutation-time schema validation issue in an operator-provided candidate value.
/// </summary>
public sealed record ConfigurationCandidateValidationIssue
{
    /// <summary>
    /// Gets the owning configuration definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the display name of the owning configuration definition.
    /// </summary>
    public required string DefinitionDisplayName { get; init; }

    /// <summary>
    /// Gets the logical path of the invalid candidate node.
    /// </summary>
    public required LogicalPath LogicalPath { get; init; }

    /// <summary>
    /// Gets the display name of the invalid schema node.
    /// </summary>
    public required string NodeDisplayName { get; init; }

    /// <summary>
    /// Gets the human-readable validation problem.
    /// </summary>
    public required string Problem { get; init; }

    /// <summary>
    /// Gets the display-safe candidate value. Sensitive and missing values are represented by metadata instead.
    /// </summary>
    public string? CandidateDisplayValue { get; init; }

    /// <summary>
    /// Gets whether the candidate value is missing.
    /// </summary>
    public bool IsMissing { get; init; }

    /// <summary>
    /// Gets whether the candidate value was redacted because the schema marks the node sensitive.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Gets the schema validation rules that constrain the candidate value.
    /// </summary>
    public IReadOnlyList<ConfigurationValidationRule> ValidationRules { get; init; } = [];
}
