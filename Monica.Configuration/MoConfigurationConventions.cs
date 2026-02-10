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

    #region 面板
    /// <summary>
    /// 更新指定配置
    /// </summary>
    public static string DashboardConfigUpdate { get; set; } = "/configuration/update";
    /// <summary>
    /// 获取微服务配置状态
    /// </summary>
    public static string DashboardAllConfigStatus { get; set; } = "/configuration/status";
    /// <summary>
    /// 获取指定配置类状态
    /// </summary>
    public static string DashboardConfigStatus { get; set; } = "/configuration/config/status";
    /// <summary>
    /// 获取指定配置状态
    /// </summary>
    public static string DashboardOptionItemStatus { get; set; } = "/configuration/option/status";
    /// <summary>
    /// 获取配置类历史
    /// </summary>
    public static string DashboardConfigHistory { get; set; } = "/configuration/history";
    /// <summary>
    /// 回滚配置类
    /// </summary>
    public static string DashboardConfigRollback { get; set; } = "/configuration/rollback";
    #endregion

}