namespace Monica.Configuration.Abstractions;

/// <summary>
/// Project catalog for configuration management
/// Provides domain and display name mappings for projects
/// </summary>
public interface IConfigurationProjectCatalog
{
    /// <summary>
    /// Get domain name by project name (e.g., "FlightService.API" → "Flight")
    /// </summary>
    string GetDomainName(string projectName);

    /// <summary>
    /// Get domain display title by domain name (e.g., "Flight" -> "Flight Domain")
    /// </summary>
    string GetDomainTitle(string domainName);

    /// <summary>
    /// Get project display name for UI (e.g., "FlightService.API" -> "Flight Service")
    /// </summary>
    string GetProjectDisplayName(string projectName);

    /// <summary>
    /// Check if project belongs to current service's domain
    /// </summary>
    bool IsCurrentDomain(string projectName);

    /// <summary>
    /// Get current service's domain name
    /// </summary>
    string CurrentDomainName { get; }

    /// <summary>
    /// Get current service's AppId
    /// </summary>
    string CurrentAppId { get; }
}
