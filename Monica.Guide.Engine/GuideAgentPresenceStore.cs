using System.Text.Json;

namespace Monica.Guide;

/// <summary>
/// One persisted agent-host observation: the detected hosts, their environments and versions,
/// when the detection ran, and enumeration warnings when parts of the machine (for example
/// WSL) could not be probed. Interactive surfaces render this snapshot instead of probing
/// hosts on every load; only an explicit re-detection replaces it.
/// </summary>
public sealed record GuideAgentPresenceSnapshot(
    int SchemaVersion,
    DateTimeOffset DetectedAt,
    IReadOnlyList<SetupAgentPresence> Presence,
    IReadOnlyList<string> Warnings)
{
    public const int CurrentSchemaVersion = 1;
}

/// <summary>
/// Loads and persists <see cref="GuideAgentPresenceSnapshot"/> in the engine state root.
/// Host observation is advisory, so an unreadable file is treated like a missing one: the
/// next detection rewrites a valid snapshot instead of failing the wizard.
/// </summary>
public static class GuideAgentPresenceStore
{
    public static GuideAgentPresenceSnapshot? Load(GuidePaths enginePaths)
    {
        ArgumentNullException.ThrowIfNull(enginePaths);
        try
        {
            if (!File.Exists(enginePaths.AgentPresenceFile))
            {
                return null;
            }

            return JsonSerializer.Deserialize<GuideAgentPresenceSnapshot>(
                File.ReadAllBytes(enginePaths.AgentPresenceFile),
                GuidePlanning.JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save(GuidePaths enginePaths, GuideAgentPresenceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(enginePaths);
        ArgumentNullException.ThrowIfNull(snapshot);
        Directory.CreateDirectory(enginePaths.StateDirectory);
        File.WriteAllBytes(enginePaths.AgentPresenceFile, GuidePlanning.JsonBytes(snapshot));
    }
}
