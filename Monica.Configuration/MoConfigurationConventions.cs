namespace Monica.Configuration;

/// <summary>
/// Provides a set of conventions for managing configuration-related endpoints 
/// and operations.
/// </summary>
/// <remarks>
/// This class contains predefined constants representing the routes for various 
/// configuration management operations, such as updating, retrieving status, 
/// fetching history, and rolling back configurations. These conventions are 
/// utilized across the platform to ensure consistency and ease of integration.
/// </remarks>
public static class MoConfigurationConventions
{

    #region Dashboard
    /// <summary>
    /// Updates a specific configuration.
    /// </summary>
    public static string DashboardConfigUpdate { get; set; } = "/configuration/update";
    /// <summary>
    /// Gets configuration status for all microservices.
    /// </summary>
    public static string DashboardAllConfigStatus { get; set; } = "/configuration/status";
    /// <summary>
    /// Gets status for a specific configuration class.
    /// </summary>
    public static string DashboardConfigStatus { get; set; } = "/configuration/config/status";
    /// <summary>
    /// Gets status for a specific configuration item.
    /// </summary>
    public static string DashboardOptionItemStatus { get; set; } = "/configuration/option/status";
    /// <summary>
    /// Gets configuration class history.
    /// </summary>
    public static string DashboardConfigHistory { get; set; } = "/configuration/history";
    /// <summary>
    /// Rolls back a configuration class.
    /// </summary>
    public static string DashboardConfigRollback { get; set; } = "/configuration/rollback";
    #endregion

}
