namespace Monica.Configuration.Models;

/// <summary>
/// Indicates whether the configuration dashboard is operating in local mode or configuration-center mode.
/// </summary>
public enum ConfigurationDashboardMode
{
    /// <summary>
    /// The host manages only its own local configuration.
    /// </summary>
    Local,

    /// <summary>
    /// The host aggregates configuration data from multiple services.
    /// </summary>
    Center
}
