using Microsoft.AspNetCore.Builder;

namespace MoLibrary.Framework.Modules;


public static class ModuleFrameworkMonitorBuilderExtensions
{
    public static ModuleFrameworkMonitorGuide ConfigModuleFrameworkMonitor(this WebApplicationBuilder builder,
        Action<ModuleFrameworkMonitorOption>? action = null)
    {
        return new ModuleFrameworkMonitorGuide().Register(action);
    }
}