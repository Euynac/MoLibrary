using Monica.Configuration.Models;
using Monica.Configuration.UI.State;

namespace Monica.Configuration.UI.Support;

internal static class ConfigurationPendingChangeTargetExtensions
{
    /// <summary>
    /// Resolves the mutation target and its matching concurrency baseline as one invariant.
    /// </summary>
    /// <remarks>
    /// A missing source means the value is not owned by a known external provider, so the mutation falls back to
    /// Monica's effective store and must still carry the effective document version.
    /// </remarks>
    public static PendingChange WithTarget(
        this PendingChange change,
        ConfigurationDefinition definition,
        ConfigurationSourceDescriptor? source,
        long? effectiveStoreVersion)
    {
        if (source is not { Kind: not ConfigurationSourceKind.MonicaEffectiveStore })
        {
            return change with
            {
                TargetKind = ConfigurationMutationTargetKind.MonicaEffectiveStore,
                ExpectedValueVersion = effectiveStoreVersion,
                SourceKey = null,
                SourceDisplayName = null,
                SourceProviderType = null,
                SourcePhysicalPath = null,
                SourceConfigurationPath = null
            };
        }

        return change with
        {
            TargetKind = ConfigurationMutationTargetKind.ExternalConfigurationSource,
            ExpectedValueVersion = null,
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
