using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Support;

internal static class ConfigurationUnifiedVersionDisplayFormatter
{
    public static string VersionText(ConfigurationUnifiedVersionSummary version)
    {
        return $"{VersionLabel(version)} · {CreatedTimeText(version)}";
    }

    public static string VersionLabel(ConfigurationUnifiedVersionSummary version)
    {
        return ConfigurationUnifiedVersionJsonFormatter.VersionLabel(version.Version);
    }

    public static string CreatedTimeText(ConfigurationUnifiedVersionSummary version)
    {
        return version.CreatedTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }

    public static string DefinitionTooltip(ConfigurationUnifiedVersionSummary version, string emptyText)
    {
        return version.DefinitionKeys.Count == 0
            ? emptyText
            : string.Join(Environment.NewLine, version.DefinitionKeys);
    }

    public static string OperatorText(ConfigurationUnifiedVersionSummary version, string emptyText)
    {
        return OperatorTextOrNull(version) ?? emptyText;
    }

    public static string? OperatorTextOrNull(ConfigurationUnifiedVersionSummary version)
    {
        return !string.IsNullOrWhiteSpace(version.ModifierName)
            ? version.ModifierName!
            : !string.IsNullOrWhiteSpace(version.ModifierId)
                ? version.ModifierId!
                : null;
    }

    public static string ValueOrEmpty(string? value, string emptyText)
    {
        return string.IsNullOrWhiteSpace(value) ? emptyText : value;
    }
}
