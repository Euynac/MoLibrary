using System.Text.Json;

namespace Monica.Guide;

/// <summary>User-tunable guide behavior. Defaults keep the global-first guide-skill model on.</summary>
public sealed record GuidePreferences(int SchemaVersion, bool GlobalGuideSkill)
{
    public const int CurrentSchemaVersion = 1;

    public static GuidePreferences Default { get; } = new(CurrentSchemaVersion, GlobalGuideSkill: true);
}

/// <summary>Loads and persists <see cref="GuidePreferences"/> under the product data root.</summary>
public static class GuidePreferencesStore
{
    public static GuidePreferences Load(AgentProductDefinition definition, AgentProductPaths? paths = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        paths ??= AgentProductPaths.ForCurrentUser(definition);
        if (!File.Exists(paths.GuidePreferencesFile))
        {
            return GuidePreferences.Default;
        }

        var preferences = JsonSerializer.Deserialize<GuidePreferences>(
                              File.ReadAllBytes(paths.GuidePreferencesFile),
                              GuidePlanning.JsonOptions)
                          ?? throw new InvalidDataException("The guide preferences file is empty.");
        if (preferences.SchemaVersion != GuidePreferences.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported guide preferences schema {preferences.SchemaVersion}.");
        }

        return preferences;
    }

    public static void Save(AgentProductDefinition definition, GuidePreferences preferences, AgentProductPaths? paths = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(preferences);
        paths ??= AgentProductPaths.ForCurrentUser(definition);
        Directory.CreateDirectory(paths.ConfigurationDirectory);
        File.WriteAllBytes(
            paths.GuidePreferencesFile,
            GuidePlanning.JsonBytes(preferences with { SchemaVersion = GuidePreferences.CurrentSchemaVersion }));
    }
}
