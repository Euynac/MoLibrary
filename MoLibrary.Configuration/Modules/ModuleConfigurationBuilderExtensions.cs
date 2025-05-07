using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Configuration.Modules;

public static class ModuleConfigurationBuilderExtensions
{
    public static ModuleConfigurationGuide AddMoModuleConfiguration(this IServiceCollection services, Action<ModuleConfigurationOption>? action = null)
    {
        return new ModuleConfigurationGuide().Register(action);
    }
}