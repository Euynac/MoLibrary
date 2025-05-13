using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.DomainDrivenDesign.Modules;


public static class ModuleSwaggerBuilderExtensions
{
    public static ModuleSwaggerGuide AddMoModuleSwagger(this WebApplicationBuilder builder,
        Action<ModuleSwaggerOption>? action = null)
    {
        return new ModuleSwaggerGuide().Register(action);
    }
}