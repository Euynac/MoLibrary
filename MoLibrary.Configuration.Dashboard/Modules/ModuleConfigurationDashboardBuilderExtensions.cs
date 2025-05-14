using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Configuration.Dashboard.Modules;


public static class ModuleConfigurationDashboardBuilderExtensions
{
    public static ModuleConfigurationDashboardGuide ConfigModuleConfigurationDashboard(this WebApplicationBuilder builder,
        Action<ModuleConfigurationDashboardOption>? action = null)
    {
        return new ModuleConfigurationDashboardGuide().Register(action);
    }
}