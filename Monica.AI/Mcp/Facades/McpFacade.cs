using Monica.AI.AgentCapabilities.Abstractions;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.Mcp.Abstractions;
using Monica.AI.Mcp.Models;
using Monica.AI.Mcp.Services;
using Monica.Core.Extensions;
using Monica.Core.Results;

namespace Monica.AI.Mcp.Facades;

/// <summary>
/// UI-facing entry point for MCP connectivity and user-managed external client profiles.
/// </summary>
public sealed class McpFacade
{
    private readonly IExternalMcpClientProfileStore profileStore;
    private readonly MonicaMcpCatalog catalog;
    private readonly IAgentCapabilityService capabilityService;

    internal McpFacade(
        IExternalMcpClientProfileStore profileStore,
        MonicaMcpCatalog catalog,
        IAgentCapabilityService capabilityService)
    {
        this.profileStore = profileStore;
        this.catalog = catalog;
        this.capabilityService = capabilityService;
    }

    /// <summary>
    /// Tests connectivity or local readiness for an MCP catalog entry.
    /// </summary>
    public async Task<Res<McpConnectivityTestResult>> TestConnectivityAsync(
        string name,
        CancellationToken ct = default)
    {
        return await ExecuteAsync(async () =>
            await catalog.TestConnectivityAsync(name, ct)
            ?? throw new KeyNotFoundException($"MCP entry '{name}' was not found."));
    }

    /// <summary>
    /// Tests a draft external MCP client profile without saving it.
    /// </summary>
    public Task<Res<McpConnectivityTestResult>> TestExternalProfileAsync(
        ExternalMcpClientProfile profile,
        CancellationToken ct = default)
    {
        return ExecuteAsync(() =>
        {
            ArgumentNullException.ThrowIfNull(profile);
            return catalog.TestExternalProfileAsync(
                profile.Normalize(ExternalMcpClientProfileOrigin.User),
                ct);
        });
    }

    /// <summary>
    /// Saves a user-managed external MCP client profile and invalidates cached clients and tools.
    /// </summary>
    public Task<Res<AgentCapabilityManagementInfo>> SaveExternalProfileAsync(
        ExternalMcpClientProfile profile,
        CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(profile);
            var normalized = profile.Normalize(ExternalMcpClientProfileOrigin.User);
            normalized.Validate();
            var management = await capabilityService.GetManagementInfoAsync(ct);
            var conflictingEntry = management.McpEntries.FirstOrDefault(entry =>
                string.Equals(entry.Name, normalized.Name, StringComparison.OrdinalIgnoreCase)
                && !entry.IsUserManaged);
            if (conflictingEntry is not null)
            {
                throw new InvalidOperationException(
                    $"MCP entry '{normalized.Name}' is code-defined or local and cannot be overwritten from the UI.");
            }

            await profileStore.SaveAsync(normalized, ct);
            return await capabilityService.RefreshAsync(ct);
        });
    }

    /// <summary>
    /// Deletes a user-managed external MCP client profile and invalidates cached clients and tools.
    /// </summary>
    public Task<Res<AgentCapabilityManagementInfo>> DeleteExternalProfileAsync(
        string name,
        CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (!await profileStore.DeleteAsync(name, ct))
            {
                throw new KeyNotFoundException($"External MCP profile '{name}' was not found.");
            }

            return await capabilityService.RemoveEntryOverrideAsync(AgentCapabilityKind.Mcp, name, ct);
        });
    }

    private static async Task<Res<T>> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
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
}
