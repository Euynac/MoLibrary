using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Configuration.Dashboard.Modules;

public class ModuleConfigurationDashboardOption : IMoModuleControllerOption<ModuleConfigurationDashboard>
{
    /// <summary>
    /// 设定当前微服务是配置中心
    /// </summary>
    public bool ThisIsDashboard { get; set; }
    public string? SwaggerGroupName { get; set; }
}