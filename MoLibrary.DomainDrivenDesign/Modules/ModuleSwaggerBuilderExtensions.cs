using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.DomainDrivenDesign.Modules;


public static class ModuleSwaggerBuilderExtensions
{
    public static ModuleSwaggerGuide AddMoModuleSwagger(this IServiceCollection services,
        Action<ModuleSwaggerOption>? action = null)
    {
        return new ModuleSwaggerGuide().Register(action);
    }
}