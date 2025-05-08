using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.DependencyInjection.Modules;


public static class ModuleDependencyInjectionBuilderExtensions
{
    public static ModuleDependencyInjectionGuide AddMoModuleDependencyInjection(this IServiceCollection services,
        Action<ModuleDependencyInjectionOption>? action = null)
    {
        return new ModuleDependencyInjectionGuide().Register(action);
    }
}