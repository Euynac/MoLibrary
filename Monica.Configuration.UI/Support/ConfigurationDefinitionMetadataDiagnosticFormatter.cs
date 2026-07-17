using Microsoft.Extensions.Localization;
using Monica.Configuration.Models;
using Monica.Configuration.UI.Localization;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Formats persisted configuration definition metadata diagnostics for operator-facing UI surfaces.
/// </summary>
internal static class ConfigurationDefinitionMetadataDiagnosticFormatter
{
    /// <summary>
    /// Formats the definition availability state.
    /// </summary>
    /// <param name="availability">The definition availability to display.</param>
    /// <param name="localizer">The localizer used for user-facing copy.</param>
    /// <returns>A localized availability label.</returns>
    public static string AvailabilityLabel(
        ConfigurationDefinitionAvailability availability,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        return availability switch
        {
            ConfigurationDefinitionAvailability.AvailableWithMetadataFault =>
                localizer["State:Definitions:MetadataFault"],
            ConfigurationDefinitionAvailability.Unavailable =>
                localizer["State:Definitions:Unavailable"],
            _ => string.Empty
        };
    }

    /// <summary>
    /// Formats a persisted metadata issue category without exposing raw technical details.
    /// </summary>
    /// <param name="kind">The metadata issue category.</param>
    /// <param name="localizer">The localizer used for user-facing copy.</param>
    /// <returns>A localized issue label.</returns>
    public static string IssueLabel(
        ConfigurationDefinitionMetadataIssueKind kind,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        return kind switch
        {
            ConfigurationDefinitionMetadataIssueKind.InvalidEnvelope =>
                localizer["State:Definitions:MetadataIssues:InvalidEnvelope"],
            ConfigurationDefinitionMetadataIssueKind.InvalidReloadBehavior =>
                localizer["State:Definitions:MetadataIssues:InvalidReloadBehavior"],
            ConfigurationDefinitionMetadataIssueKind.InvalidSchemaJson =>
                localizer["State:Definitions:MetadataIssues:InvalidSchemaJson"],
            ConfigurationDefinitionMetadataIssueKind.SchemaHashMismatch =>
                localizer["State:Definitions:MetadataIssues:SchemaHashMismatch"],
            ConfigurationDefinitionMetadataIssueKind.OutdatedSchemaContract =>
                localizer["State:Definitions:MetadataIssues:OutdatedSchemaContract"],
            _ => localizer["State:Definitions:MetadataIssues:InvalidSchema"]
        };
    }
}
