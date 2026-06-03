using Monica.Configuration.Models;
using Monica.Configuration.UI.State;

namespace Monica.Configuration.UI.Support;

internal static class ConfigurationPendingChangeTargetExtensions
{
    public static PendingChange WithTarget(
        this PendingChange change,
        ConfigurationDefinition definition,
        ConfigurationSourceDescriptor? source)
    {
        if (source is not { Kind: not ConfigurationSourceKind.MonicaEffectiveStore })
        {
            return change;
        }

        return change with
        {
            TargetKind = ConfigurationMutationTargetKind.ExternalConfigurationSource,
            SourceKey = source.SourceKey,
            SourceDisplayName = source.DisplayName,
            SourceProviderType = source.ProviderType,
            SourcePhysicalPath = source.PhysicalPath,
            SourceConfigurationPath = ProjectPath(definition, change.LogicalPath)
        };
    }

    private static string ProjectPath(ConfigurationDefinition definition, LogicalPath logicalPath)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(definition.SectionPath))
        {
            parts.Add(definition.SectionPath);
        }

        parts.AddRange(logicalPath.Segments.Select(segment => segment.Value));
        return string.Join(':', parts);
    }
}
