using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Configuration.Modules;

public static class ModuleConfigurationBuilderExtensions
{
    public static ModuleConfigurationGuide ConfigModuleConfiguration(this WebApplicationBuilder builder, Action<ModuleConfigurationOption>? action = null)
    {
        return new ModuleConfigurationGuide().Register(action);
    }
}