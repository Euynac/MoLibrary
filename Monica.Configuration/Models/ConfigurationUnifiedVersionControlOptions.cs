namespace Monica.Configuration.Models;

/// <summary>
/// Controls unified configuration version capture.
/// </summary>
public sealed class ConfigurationUnifiedVersionControlOptions
{
    /// <summary>
    /// Gets or sets whether unified configuration version capture is enabled.
    /// </summary>
    /// <remarks>
    /// The default is <c>false</c>. Enable it through the unified-version registration extension
    /// and register at least one filter so the captured definition set is explicit.
    /// </remarks>
    public bool Enabled { get; set; }
}
