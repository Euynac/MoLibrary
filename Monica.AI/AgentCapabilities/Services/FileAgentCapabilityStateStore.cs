using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.AI.AgentCapabilities.Abstractions;
using Monica.AI.AgentCapabilities.Models;
using Monica.Modules;
using Monica.Tool.Runtime;

namespace Monica.AI.AgentCapabilities.Services;

/// <summary>
/// File-backed runtime state store for agent capability enablement.
/// </summary>
public sealed class FileAgentCapabilityStateStore(
    IOptions<ModuleAIOption> options,
    ILogger<FileAgentCapabilityStateStore> logger) : IAgentCapabilityStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath =
        RuntimePathHelper.GetRelativePathInRunningPath(options.Value.CapabilityStateStoreFilePath);

    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc />
    public async Task<AgentCapabilityState> LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return await ReadAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<AgentCapabilityState> UpdateAsync(
        Func<AgentCapabilityState, bool> update,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        await _lock.WaitAsync(ct);
        try
        {
            var state = await ReadAsync(ct);
            if (!update(state))
            {
                return Normalize(state);
            }

            state.Revision = Math.Max(1, state.Revision) + 1;
            state = Normalize(state);
            await WriteAsync(state, ct);
            return state;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<AgentCapabilityState> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            return new AgentCapabilityState();
        }

        var json = await File.ReadAllTextAsync(_filePath, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AgentCapabilityState();
        }

        try
        {
            return Normalize(JsonSerializer.Deserialize<AgentCapabilityState>(json, JsonOptions)
                             ?? new AgentCapabilityState());
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to read agent capability state from {Path}; using defaults.", _filePath);
            return new AgentCapabilityState();
        }
    }

    private async Task WriteAsync(AgentCapabilityState state, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, ct);
        logger.LogDebug("Saved agent capability state to {Path}", _filePath);
    }

    private static AgentCapabilityState Normalize(AgentCapabilityState state)
    {
        state.Revision = Math.Max(1, state.Revision);
        state.SkillEntries = NormalizeEntries(state.SkillEntries);
        state.McpEntries = NormalizeEntries(state.McpEntries);
        return state;
    }

    private static Dictionary<string, bool> NormalizeEntries(Dictionary<string, bool>? entries)
    {
        return new Dictionary<string, bool>(
            (entries ?? [])
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Key))
            .Select(static entry => new KeyValuePair<string, bool>(entry.Key.Trim(), entry.Value)),
            StringComparer.OrdinalIgnoreCase);
    }
}
