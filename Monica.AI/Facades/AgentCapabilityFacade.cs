using Monica.AI.AgentCapabilities.Abstractions;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.Mcp.Abstractions;
using Monica.AI.Mcp.Models;
using Monica.AI.Mcp.Services;
using Monica.AI.Skills.Services;
using Monica.Core.Extensions;
using Monica.Core.Results;

namespace Monica.AI.Facades;

/// <summary>
/// Host-facing facade for inspecting and managing agent-facing Skill and MCP capabilities.
/// </summary>
public sealed class AgentCapabilityFacade(
    IAgentCapabilityStateStore stateStore,
    IExternalMcpClientProfileStore mcpProfileStore,
    MonicaSkillCatalog skillCatalog,
    MonicaMcpCatalog mcpCatalog)
{
    /// <summary>
    /// Gets the current management view for all discovered capabilities.
    /// </summary>
    public async Task<Res<AgentCapabilityManagementInfo>> GetManagementInfoAsync(CancellationToken ct = default)
    {
        try
        {
            var state = await stateStore.LoadAsync(ct);
            var skills = skillCatalog.GetEntries(state);
            var mcpEntries = await mcpCatalog.GetCapabilityEntriesAsync(state, ct);
            return new AgentCapabilityManagementInfo(
                state.SkillsEnabled,
                state.McpEnabled,
                state.Revision,
                skills,
                mcpEntries);
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    /// <summary>
    /// Gets slash-command candidates from enabled and disabled capabilities.
    /// </summary>
    public async Task<Res<IReadOnlyList<AgentCapabilityReferenceCandidate>>> GetReferenceCandidatesAsync(
        string? query = null,
        CancellationToken ct = default)
    {
        var managementResult = await GetManagementInfoAsync(ct);
        if (managementResult.IsFailed(out var error, out var management))
        {
            return error;
        }

        var candidates = management.Entries
            .Select(static entry => entry.ToReferenceCandidate())
            .Where(candidate => Matches(candidate, query))
            .OrderByDescending(static candidate => candidate.IsEnabled)
            .ThenBy(static candidate => candidate.Kind)
            .ThenBy(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return candidates;
    }

    /// <summary>
    /// Sets the global runtime switch for a capability catalog.
    /// </summary>
    public async Task<Res<AgentCapabilityManagementInfo>> SetCatalogEnabledAsync(
        AgentCapabilityKind kind,
        bool isEnabled,
        CancellationToken ct = default)
    {
        await stateStore.UpdateAsync(state =>
        {
            if (kind == AgentCapabilityKind.Skill)
            {
                if (state.SkillsEnabled == isEnabled)
                {
                    return false;
                }

                state.SkillsEnabled = isEnabled;
                return true;
            }

            if (state.McpEnabled == isEnabled)
            {
                return false;
            }

            state.McpEnabled = isEnabled;
            return true;
        }, ct);

        return await GetManagementInfoAsync(ct);
    }

    /// <summary>
    /// Sets the runtime switch for one capability entry.
    /// </summary>
    public async Task<Res<AgentCapabilityManagementInfo>> SetEntryEnabledAsync(
        AgentCapabilityKind kind,
        string name,
        bool isEnabled,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await stateStore.UpdateAsync(state =>
        {
            var entries = kind == AgentCapabilityKind.Skill ? state.SkillEntries : state.McpEntries;
            if (entries.TryGetValue(name, out var current) && current == isEnabled)
            {
                return false;
            }

            entries[name.Trim()] = isEnabled;
            return true;
        }, ct);

        return await GetManagementInfoAsync(ct);
    }

    /// <summary>
    /// Tests connectivity or readiness for an MCP entry.
    /// </summary>
    public async Task<Res<McpConnectivityTestResult>> TestMcpConnectivityAsync(
        string name,
        CancellationToken ct = default)
    {
        try
        {
            var result = await mcpCatalog.TestConnectivityAsync(name, ct);
            return result is null
                ? Res.Fail($"MCP entry '{name}' was not found.")
                : result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    /// <summary>
    /// Tests a draft external MCP client profile without saving it.
    /// </summary>
    public async Task<Res<McpConnectivityTestResult>> TestExternalMcpProfileAsync(
        ExternalMcpClientProfile profile,
        CancellationToken ct = default)
    {
        try
        {
            return await mcpCatalog.TestExternalProfileAsync(profile.Normalize(ExternalMcpClientProfileOrigin.User), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    /// <summary>
    /// Saves a UI-managed external MCP client profile and refreshes the management view.
    /// </summary>
    public async Task<Res<AgentCapabilityManagementInfo>> SaveExternalMcpProfileAsync(
        ExternalMcpClientProfile profile,
        CancellationToken ct = default)
    {
        try
        {
            var normalized = profile.Normalize(ExternalMcpClientProfileOrigin.User);
            normalized.Validate();
            var current = await GetManagementInfoAsync(ct);
            if (current.IsFailed(out var currentError, out var management))
            {
                return currentError;
            }

            var conflictingEntry = management.McpEntries.FirstOrDefault(entry =>
                string.Equals(entry.Name, normalized.Name, StringComparison.OrdinalIgnoreCase)
                && !entry.IsUserManaged);
            if (conflictingEntry is not null)
            {
                return Res.Fail($"MCP entry '{normalized.Name}' is code-defined or local and cannot be overwritten from the UI.");
            }

            await mcpProfileStore.SaveAsync(normalized, ct);
            await mcpCatalog.InvalidateExternalEntriesAsync(ct);
            await TouchRevisionAsync(ct);
            return await GetManagementInfoAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    /// <summary>
    /// Deletes a UI-managed external MCP client profile and refreshes the management view.
    /// </summary>
    public async Task<Res<AgentCapabilityManagementInfo>> DeleteExternalMcpProfileAsync(
        string name,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            var deleted = await mcpProfileStore.DeleteAsync(name, ct);
            if (!deleted)
            {
                return Res.Fail($"External MCP profile '{name}' was not found.");
            }

            await mcpCatalog.InvalidateExternalEntriesAsync(ct);
            await stateStore.UpdateAsync(state =>
            {
                state.McpEntries.Remove(name.Trim());
                return true;
            }, ct);
            return await GetManagementInfoAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    private async Task TouchRevisionAsync(CancellationToken ct)
    {
        await stateStore.UpdateAsync(_ => true, ct);
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
