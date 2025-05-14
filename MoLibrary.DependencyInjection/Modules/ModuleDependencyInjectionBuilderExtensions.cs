using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.DependencyInjection.Modules;


public static class ModuleDependencyInjectionBuilderExtensions
{
    public static ModuleDependencyInjectionGuide ConfigModuleDependencyInjection(this WebApplicationBuilder builder,
        Action<ModuleDependencyInjectionOption>? action = null)
    {
        return new ModuleDependencyInjectionGuide().Register(action);
    }
}