using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Framework.Modules;


public static class ModuleFrameworkMonitorBuilderExtensions
{
    public static ModuleFrameworkMonitorGuide AddMoModuleFrameworkMonitor(this WebApplicationBuilder builder,
        Action<ModuleFrameworkMonitorOption>? action = null)
    {
        return new ModuleFrameworkMonitorGuide().Register(action);
    }
}