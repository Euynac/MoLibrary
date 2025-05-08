using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Framework.Modules;


public static class ModuleFrameworkMonitorBuilderExtensions
{
    public static ModuleFrameworkMonitorGuide AddMoModuleFrameworkMonitor(this IServiceCollection services,
        Action<ModuleFrameworkMonitorOption>? action = null)
    {
        return new ModuleFrameworkMonitorGuide().Register(action);
    }
}