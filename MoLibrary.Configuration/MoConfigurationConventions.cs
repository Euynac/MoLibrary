namespace MoLibrary.Configuration;

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
    public static string DashboardConfigUpdate = "/configuration/update";
    /// <summary>
    /// 获取微服务配置状态
    /// </summary>
    public static string DashboardAllConfigStatus = "/configuration/status";

    /// <summary>
    /// 获取指定配置类状态
    /// </summary>
    public static string DashboardConfigStatus = "/configuration/config/status";
    /// <summary>
    /// 获取指定配置状态
    /// </summary>
    public static string DashboardOptionItemStatus = "/configuration/option/status";
    /// <summary>
    /// 获取配置类历史
    /// </summary>
    public static string DashboardConfigHistory = "/configuration/history";
    /// <summary>
    /// 回滚配置类
    /// </summary>
    public static string DashboardConfigRollback = "/configuration/rollback";
    #endregion

}