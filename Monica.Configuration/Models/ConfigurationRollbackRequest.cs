namespace Monica.Configuration.Models;

/// <summary>
/// Request for rolling a configuration back to a previous version.
/// </summary>
public class ConfigurationRollbackRequest
{
    /// <summary>
    /// Configuration class or option key.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Application ID that owns the configuration.
    /// </summary>
    public required string AppId { get; set; }

    /// <summary>
    /// Target history version.
    /// </summary>
    public required string Version { get; set; }
}
