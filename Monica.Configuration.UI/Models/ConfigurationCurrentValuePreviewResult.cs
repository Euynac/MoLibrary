namespace Monica.Configuration.UI.Models;

/// <summary>
/// Result returned by the current-value preview dialog.
/// </summary>
public enum ConfigurationCurrentValuePreviewResult
{
    /// <summary>
    /// The operator only closed the preview.
    /// </summary>
    Closed,

    /// <summary>
    /// The operator requested JSON editing for the previewed value.
    /// </summary>
    EditJson
}
