using System.Text.Json;

namespace Monica.Guide;

/// <summary>
/// How agent-driven upstream issue reporting may proceed on this machine. The mode bounds
/// preparation only; a persisted value never authorizes a remote action.
/// </summary>
public enum GuideIssueReportingMode
{
    /// <summary>Agents may draft local issue artifacts; every remote action still requires current-session approval.</summary>
    Prepare,

    /// <summary>Agents must ask before preparing any issue artifact.</summary>
    Ask,

    /// <summary>Agents must not prepare or create issue artifacts at all.</summary>
    Never
}

/// <summary>Machine-global issue-reporting preference, observed identically by every product guide.</summary>
public sealed record GuideIssuePreferences(int SchemaVersion, GuideIssueReportingMode IssueReporting)
{
    public const int CurrentSchemaVersion = 1;

    public static GuideIssuePreferences Default { get; } = new(CurrentSchemaVersion, GuideIssueReportingMode.Prepare);
}

/// <summary>Human-facing meaning of one issue-reporting mode, shared by the CLI and the wizard.</summary>
public static class GuideIssueReportingText
{
    public static string Describe(this GuideIssueReportingMode mode) => mode switch
    {
        GuideIssueReportingMode.Prepare =>
            "Agents may draft local issue artifacts; every remote action still requires current-session approval.",
        GuideIssueReportingMode.Ask => "Agents must ask before preparing any issue artifact.",
        _ => "Agents must not prepare or create issue artifacts at all."
    };
}

/// <summary>Loads and persists <see cref="GuideIssuePreferences"/> in the engine state root.</summary>
public static class GuideIssuePreferencesStore
{
    public static GuideIssuePreferences Load(GuidePaths enginePaths)
    {
        ArgumentNullException.ThrowIfNull(enginePaths);
        if (!File.Exists(enginePaths.IssuePreferencesFile))
        {
            return GuideIssuePreferences.Default;
        }

        var preferences = JsonSerializer.Deserialize<GuideIssuePreferences>(
                              File.ReadAllBytes(enginePaths.IssuePreferencesFile),
                              GuidePlanning.JsonOptions)
                          ?? throw new InvalidDataException("The issue preferences file is empty.");
        if (preferences.SchemaVersion != GuideIssuePreferences.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported issue preferences schema {preferences.SchemaVersion}.");
        }

        return preferences;
    }

    public static void Save(GuidePaths enginePaths, GuideIssuePreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(enginePaths);
        ArgumentNullException.ThrowIfNull(preferences);
        Directory.CreateDirectory(enginePaths.StateDirectory);
        File.WriteAllBytes(
            enginePaths.IssuePreferencesFile,
            GuidePlanning.JsonBytes(preferences with { SchemaVersion = GuideIssuePreferences.CurrentSchemaVersion }));
    }
}
