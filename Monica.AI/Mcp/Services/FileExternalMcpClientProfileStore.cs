using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.AI.Mcp.Abstractions;
using Monica.AI.Mcp.Models;
using Monica.Modules;
using Monica.Tool.Runtime;

namespace Monica.AI.Mcp.Services;

/// <summary>
/// File-backed store for runtime-managed external MCP client profiles.
/// </summary>
internal sealed class FileExternalMcpClientProfileStore(
    IOptions<ModuleMcpOption> options,
    ILogger<FileExternalMcpClientProfileStore> logger) : IExternalMcpClientProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath =
        RuntimePathHelper.GetRelativePathInRunningPath(options.Value.ExternalMcpClientProfileStoreFilePath);

    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExternalMcpClientProfile>> LoadAsync(CancellationToken ct = default)
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
    public async Task SaveAsync(ExternalMcpClientProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var normalized = profile.Normalize(ExternalMcpClientProfileOrigin.User);
        normalized.Validate();

        await _lock.WaitAsync(ct);
        try
        {
            var profiles = (await ReadAsync(ct)).ToList();
            var existingIndex = profiles.FindIndex(entry =>
                string.Equals(entry.Name, normalized.Name, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                profiles[existingIndex] = normalized;
            }
            else
            {
                profiles.Add(normalized);
            }

            await WriteAsync(profiles, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await _lock.WaitAsync(ct);
        try
        {
            var profiles = (await ReadAsync(ct)).ToList();
            var removed = profiles.RemoveAll(entry =>
                string.Equals(entry.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

            if (removed == 0)
            {
                return false;
            }

            await WriteAsync(profiles, ct);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyList<ExternalMcpClientProfile>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(_filePath, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var payload = JsonSerializer.Deserialize<ExternalMcpClientProfileStorePayload>(json, JsonOptions)
                          ?? new ExternalMcpClientProfileStorePayload();
            return Normalize(payload.Profiles);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to read external MCP client profiles from {Path}; using an empty profile list.", _filePath);
            return [];
        }
    }

    private async Task WriteAsync(IReadOnlyList<ExternalMcpClientProfile> profiles, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var payload = new ExternalMcpClientProfileStorePayload
        {
            Version = 1,
            Profiles = Normalize(profiles).ToList()
        };
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, ct);
        logger.LogDebug("Saved external MCP client profiles to {Path}", _filePath);
    }

    private static IReadOnlyList<ExternalMcpClientProfile> Normalize(IEnumerable<ExternalMcpClientProfile>? profiles)
    {
        return (profiles ?? [])
            .Where(static profile => !string.IsNullOrWhiteSpace(profile.Name))
            .Select(static profile => profile.Normalize(ExternalMcpClientProfileOrigin.User))
            .GroupBy(static profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.Last())
            .OrderBy(static profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
