using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.DependencyInjection.Modules;


public static class ModuleDependencyInjectionBuilderExtensions
{
    public static ModuleDependencyInjectionGuide AddMoModuleDependencyInjection(this WebApplicationBuilder builder,
        Action<ModuleDependencyInjectionOption>? action = null)
    {
        return new ModuleDependencyInjectionGuide().Register(action);
    }
}