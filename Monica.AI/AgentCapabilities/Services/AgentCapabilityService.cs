using Monica.AI.AgentCapabilities.Abstractions;
using Monica.AI.AgentCapabilities.Models;

namespace Monica.AI.AgentCapabilities.Services;

internal sealed class AgentCapabilityService(
    IAgentCapabilityStateStore stateStore,
    IEnumerable<IAgentCapabilitySource> sources) : IAgentCapabilityService
{
    private readonly IReadOnlyList<IAgentCapabilitySource> _sources = sources.ToList();

    public async Task<AgentCapabilityManagementInfo> GetManagementInfoAsync(CancellationToken ct = default)
    {
        var state = await stateStore.LoadAsync(ct);
        var snapshots = new List<(AgentCapabilityKind Kind, AgentCapabilitySourceSnapshot Snapshot)>();
        foreach (var source in _sources)
        {
            snapshots.Add((source.Kind, await source.GetSnapshotAsync(state, ct)));
        }

        ValidateUniqueEntries(snapshots);
        var skills = SelectEntries(snapshots, AgentCapabilityKind.Skill);
        var mcpEntries = SelectEntries(snapshots, AgentCapabilityKind.Mcp);
        var fileSkillSources = snapshots
            .SelectMany(static item => item.Snapshot.FileSkillSources)
            .ToList();

        return new AgentCapabilityManagementInfo(
            state.SkillsEnabled,
            state.McpEnabled,
            state.Revision,
            skills,
            mcpEntries,
            fileSkillSources);
    }

    public async Task<IReadOnlyList<AgentCapabilityReferenceCandidate>> GetReferenceCandidatesAsync(
        string? query = null,
        CancellationToken ct = default)
    {
        var management = await GetManagementInfoAsync(ct);
        return management.Entries
            .Select(static entry => entry.ToReferenceCandidate())
            .Where(candidate => Matches(candidate, query))
            .OrderByDescending(static candidate => candidate.IsEnabled)
            .ThenBy(static candidate => candidate.Kind)
            .ThenBy(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<AgentCapabilityManagementInfo> SetCatalogEnabledAsync(
        AgentCapabilityKind kind,
        bool isEnabled,
        CancellationToken ct = default)
    {
        return UpdateAsync(state => state.SetCatalogEnabled(kind, isEnabled), kind, ct);
    }

    public Task<AgentCapabilityManagementInfo> SetEntryEnabledAsync(
        AgentCapabilityKind kind,
        string name,
        bool isEnabled,
        CancellationToken ct = default)
    {
        return UpdateAsync(state => state.SetEntryEnabled(kind, name, isEnabled), kind, ct);
    }

    public Task<AgentCapabilityManagementInfo> SetSkillMcpServerEnabledAsync(
        string skillName,
        bool isEnabled,
        CancellationToken ct = default)
    {
        return UpdateAsync(
            state => state.SetSkillMcpServerEnabled(skillName, isEnabled),
            AgentCapabilityKind.Mcp,
            ct);
    }

    public Task<AgentCapabilityManagementInfo> RemoveEntryOverrideAsync(
        AgentCapabilityKind kind,
        string name,
        CancellationToken ct = default)
    {
        return UpdateAsync(state => state.RemoveEntryOverride(kind, name), kind, ct);
    }

    public Task<AgentCapabilityManagementInfo> RefreshAsync(CancellationToken ct = default)
    {
        return UpdateAsync(_ => true, null, ct);
    }

    private async Task<AgentCapabilityManagementInfo> UpdateAsync(
        Func<AgentCapabilityState, bool> update,
        AgentCapabilityKind? invalidatedKind,
        CancellationToken ct)
    {
        var changed = false;
        await stateStore.UpdateAsync(state => changed = update(state), ct);
        if (changed)
        {
            foreach (var source in _sources.Where(source =>
                         invalidatedKind is null || source.Kind == invalidatedKind))
            {
                await source.InvalidateAsync(ct);
            }
        }

        return await GetManagementInfoAsync(ct);
    }

    private static IReadOnlyList<AgentCapabilityEntryInfo> SelectEntries(
        IEnumerable<(AgentCapabilityKind Kind, AgentCapabilitySourceSnapshot Snapshot)> snapshots,
        AgentCapabilityKind kind)
    {
        return snapshots
            .Where(item => item.Kind == kind)
            .SelectMany(static item => item.Snapshot.Entries)
            .OrderBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ValidateUniqueEntries(
        IReadOnlyList<(AgentCapabilityKind Kind, AgentCapabilitySourceSnapshot Snapshot)> snapshots)
    {
        foreach (var (kind, snapshot) in snapshots)
        {
            if (snapshot.Entries.Any(entry => entry.Kind != kind))
            {
                throw new InvalidOperationException(
                    $"Capability source '{kind}' returned an entry owned by another catalog.");
            }
        }

        var duplicateKeys = snapshots
            .SelectMany(static item => item.Snapshot.Entries)
            .GroupBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicateKeys.Count > 0)
        {
            throw new InvalidOperationException(
                "Agent capability entry keys must be unique: " + string.Join(", ", duplicateKeys) + ".");
        }
    }

    private static bool Matches(AgentCapabilityReferenceCandidate candidate, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var normalized = query.Trim();
        return Contains(candidate.Name, normalized)
               || Contains(candidate.Description, normalized)
               || candidate.ToolNames.Any(tool => Contains(tool, normalized))
               || Contains(candidate.Kind.ToString(), normalized);
    }

    private static bool Contains(string? text, string query)
    {
        return text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
    }
}
