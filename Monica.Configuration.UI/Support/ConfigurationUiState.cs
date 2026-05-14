using Monica.Configuration.Models;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Shared page state for the configuration operator console.
/// </summary>
public sealed class ConfigurationUiState
{
    /// <summary>
    /// Gets or sets the currently selected definition key.
    /// </summary>
    public string? DefinitionKey { get; set; }

    /// <summary>
    /// Gets or sets the currently selected logical path.
    /// </summary>
    public LogicalPath LogicalPath { get; set; } = LogicalPath.Root;
}
