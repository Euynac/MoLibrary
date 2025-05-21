using Microsoft.AspNetCore.Builder;

namespace MoLibrary.Configuration.Modules;

public static class ModuleConfigurationBuilderExtensions
{
    public static ModuleConfigurationGuide ConfigModuleConfiguration(this WebApplicationBuilder builder, Action<ModuleConfigurationOption>? action = null)
    {
        return new ModuleConfigurationGuide().Register(action);
    }
}