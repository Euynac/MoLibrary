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
    public static string DashboardCentreConfigUpdate = "/configuration/update";
    /// <summary>
    /// 获取所有微服务配置状态
    /// </summary>
    public static string DashboardCentreAllConfigStatus = "/configuration/status";

    /// <summary>
    /// 获取指定配置类状态
    /// </summary>
    public static string DashboardCentreConfigStatus = "/configuration/config/status";
    /// <summary>
    /// 获取指定配置状态
    /// </summary>
    public static string DashboardCentreOptionItemStatus = "/configuration/option/status";
    /// <summary>
    /// 获取配置类历史
    /// </summary>
    public static string DashboardCentreConfigHistory = "/configuration/history";
    /// <summary>
    /// 回滚配置类
    /// </summary>
    public static string DashboardCentreConfigRollback = "/configuration/rollback";
    /// <summary>
    /// 更新指定配置
    /// </summary>
    public static string DashboardClientConfigUpdate = "/option/update";

    #endregion


    /// <summary>
    /// 获取热配置状态信息
    /// </summary>
    public static string GetConfigStatus = "/option/status";

}