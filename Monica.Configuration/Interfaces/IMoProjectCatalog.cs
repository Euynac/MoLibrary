namespace Monica.Configuration.Interfaces;

/// <summary>
/// Project catalog for configuration management
/// Provides domain and display name mappings for projects
/// </summary>
public interface IMoProjectCatalog
{
    /// <summary>
    /// Get domain name by project name (e.g., "FlightService.API" → "Flight")
    /// </summary>
    string GetDomainName(string projectName);

    /// <summary>
    /// Get domain title (Chinese display name) by domain name (e.g., "Flight" → "航班子域")
    /// </summary>
    string GetDomainTitle(string domainName);

    /// <summary>
    /// Get project display name for UI (e.g., "FlightService.API" → "航班服务")
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
