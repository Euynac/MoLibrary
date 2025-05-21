using Microsoft.AspNetCore.Builder;

namespace MoLibrary.DomainDrivenDesign.Modules;


public static class ModuleSwaggerBuilderExtensions
{
    public static ModuleSwaggerGuide ConfigModuleSwagger(this WebApplicationBuilder builder,
        Action<ModuleSwaggerOption>? action = null)
    {
        return new ModuleSwaggerGuide().Register(action);
    }
}