namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Describes a slash-command candidate that can be explicitly attached to a chat message.
/// </summary>
public sealed record AgentCapabilityReferenceCandidate
{
    /// <summary>
    /// Creates a slash-command reference candidate.
    /// </summary>
    public AgentCapabilityReferenceCandidate(
        AgentCapabilityKind kind,
        string name,
        string description,
        bool isEnabled,
        string? disabledReason,
        IReadOnlyList<string> toolNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(toolNames);

        Kind = kind;
        Name = name.Trim();
        Description = description;
        IsEnabled = isEnabled;
        DisabledReason = disabledReason;
        ToolNames = toolNames;
    }

    /// <summary>
    /// Catalog that owns the capability.
    /// </summary>
    public AgentCapabilityKind Kind { get; }

    /// <summary>
    /// Stable capability name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Short discovery description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Whether the capability is globally enabled and available for explicit references.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Human-readable reason the candidate is unavailable, when disabled.
    /// </summary>
    public string? DisabledReason { get; }

    /// <summary>
    /// Tool or script names used by fuzzy matching and previews.
    /// </summary>
    public IReadOnlyList<string> ToolNames { get; }

    /// <summary>
    /// Stable UI key used by chips and completion.
    /// </summary>
    public string Key => AgentCapabilityReference.CreateKey(Kind, Name);
}
