using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Support;

internal static class ConfigurationUnifiedVersionDisplayFormatter
{
    public static string VersionText(ConfigurationUnifiedVersionSummary version)
    {
        return $"{ConfigurationUnifiedVersionJsonFormatter.VersionLabel(version.Version)} · {version.CreatedTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
    }

    public static string DefinitionTooltip(ConfigurationUnifiedVersionSummary version, string emptyText)
    {
        return version.DefinitionKeys.Count == 0
            ? emptyText
            : string.Join(Environment.NewLine, version.DefinitionKeys);
    }

    public static string OperatorText(ConfigurationUnifiedVersionSummary version, string emptyText)
    {
        return !string.IsNullOrWhiteSpace(version.ModifierName)
            ? version.ModifierName!
            : !string.IsNullOrWhiteSpace(version.ModifierId)
                ? version.ModifierId!
                : emptyText;
    }

    public static string ValueOrEmpty(string? value, string emptyText)
    {
        return string.IsNullOrWhiteSpace(value) ? emptyText : value;
    }
}
