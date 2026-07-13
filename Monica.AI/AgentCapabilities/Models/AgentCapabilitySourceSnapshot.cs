namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Immutable management snapshot contributed by one agent-capability source.
/// </summary>
public sealed record AgentCapabilitySourceSnapshot
{
    /// <summary>
    /// Creates a capability-source snapshot.
    /// </summary>
    /// <param name="entries">Entries owned by the source.</param>
    /// <param name="fileSkillSources">Optional diagnostics for file-backed skill discovery.</param>
    public AgentCapabilitySourceSnapshot(
        IReadOnlyList<AgentCapabilityEntryInfo> entries,
        IReadOnlyList<AgentCapabilityFileSkillSourceStatusInfo>? fileSkillSources = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = entries;
        FileSkillSources = fileSkillSources ?? [];
    }

    /// <summary>
    /// Entries owned by the source.
    /// </summary>
    public IReadOnlyList<AgentCapabilityEntryInfo> Entries { get; }

    /// <summary>
    /// File-backed skill discovery diagnostics supplied by the source, when applicable.
    /// </summary>
    public IReadOnlyList<AgentCapabilityFileSkillSourceStatusInfo> FileSkillSources { get; }
}
