using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Describes how the configuration dashboard should present the current host.
/// </summary>
public interface IConfigurationDashboardContext
{
    /// <summary>
    /// Gets the current dashboard mode for the host.
    /// </summary>
    ConfigurationDashboardMode Mode { get; }
}
