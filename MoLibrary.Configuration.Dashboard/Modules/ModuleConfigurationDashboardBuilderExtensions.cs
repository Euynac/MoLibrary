using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Configuration.Dashboard.Modules;


public static class ModuleConfigurationDashboardBuilderExtensions
{
    public static ModuleConfigurationDashboardGuide AddMoModuleConfigurationDashboard(this IServiceCollection services,
        Action<ModuleConfigurationDashboardOption>? action = null)
    {
        return new ModuleConfigurationDashboardGuide().Register(action);
    }
}