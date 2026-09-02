using System.Text.Json;

namespace Monica.Guide;

/// <summary>
/// Which machine-global diagnostics the managed workspace instruction block projects.
/// Changes converge with the next workspace initialization or update, mirroring the
/// global-first guide-skill preference.
/// </summary>
public sealed record GuideWorkspaceProjection(int SchemaVersion, bool SourceHints, bool IssuePolicy)
{
    public const int CurrentSchemaVersion = 1;

    public static GuideWorkspaceProjection Default { get; } = new(CurrentSchemaVersion, SourceHints: true, IssuePolicy: true);
}

/// <summary>Loads and persists <see cref="GuideWorkspaceProjection"/> in the engine state root.</summary>
public static class GuideWorkspaceProjectionStore
{
    public static GuideWorkspaceProjection Load(GuidePaths enginePaths)
    {
        ArgumentNullException.ThrowIfNull(enginePaths);
        if (!File.Exists(enginePaths.WorkspaceProjectionFile))
        {
            return GuideWorkspaceProjection.Default;
        }

        var projection = JsonSerializer.Deserialize<GuideWorkspaceProjection>(
                             File.ReadAllBytes(enginePaths.WorkspaceProjectionFile),
                             GuidePlanning.JsonOptions)
                         ?? throw new InvalidDataException("The workspace projection file is empty.");
        if (projection.SchemaVersion != GuideWorkspaceProjection.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported workspace projection schema {projection.SchemaVersion}.");
        }

        return projection;
    }

    public static void Save(GuidePaths enginePaths, GuideWorkspaceProjection projection)
    {
        ArgumentNullException.ThrowIfNull(enginePaths);
        ArgumentNullException.ThrowIfNull(projection);
        Directory.CreateDirectory(enginePaths.StateDirectory);
        File.WriteAllBytes(
            enginePaths.WorkspaceProjectionFile,
            GuidePlanning.JsonBytes(projection with { SchemaVersion = GuideWorkspaceProjection.CurrentSchemaVersion }));
    }
}
