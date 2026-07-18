namespace Monica.Configuration.UI.Models;

/// <summary>
/// Identifies the section initially displayed by the configuration definition publication dialog.
/// </summary>
public enum ConfigurationDefinitionPublicationTab
{
    /// <summary>
    /// Shows revisions that changed the effective persisted definition.
    /// </summary>
    RevisionHistory,

    /// <summary>
    /// Shows the current reload-behavior contribution reported by each logical publishing service.
    /// </summary>
    ReloadContributors
}
