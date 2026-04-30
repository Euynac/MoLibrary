using Monica.AI.AgentCapabilities.Models;

namespace Monica.AI.AgentCapabilities.Services;

/// <summary>
/// Builds short per-turn instructions for explicitly referenced agent capabilities.
/// </summary>
public static class AgentCapabilityPromptBuilder
{
    /// <summary>
    /// Builds an additional instruction block for the referenced capabilities that are currently enabled.
    /// </summary>
    public static string? BuildReferenceInstructions(
        IReadOnlyList<AgentCapabilityReference> references,
        IReadOnlyList<AgentCapabilityEntryInfo> availableEntries)
    {
        if (references.Count == 0)
        {
            return null;
        }

        var entriesByKey = availableEntries.ToDictionary(static entry => entry.Key, StringComparer.OrdinalIgnoreCase);
        var selected = references
            .Select(reference => entriesByKey.GetValueOrDefault(reference.Key))
            .OfType<AgentCapabilityEntryInfo>()
            .Where(static entry => entry.IsEnabled)
            .ToList();

        if (selected.Count == 0)
        {
            return null;
        }

        var lines = new List<string>
        {
            "The user explicitly referenced these Monica agent capabilities for this turn. Prefer them when they are relevant to the request; do not force them when they are irrelevant."
        };

        foreach (var entry in selected)
        {
            var kind = entry.Kind == AgentCapabilityKind.Skill ? "Skill" : "MCP";
            var toolNames = entry.Tools.Count == 0
                ? "no tools listed"
                : string.Join(", ", entry.Tools.Select(static tool => tool.Name));

            lines.Add($"- {kind} `{entry.Name}`: {entry.Description} Tools: {toolNames}.");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
