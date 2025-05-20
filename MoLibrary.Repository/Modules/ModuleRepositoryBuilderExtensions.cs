using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Repository.Modules;


public static class ModuleRepositoryBuilderExtensions
{
    public static ModuleRepositoryGuide ConfigModuleRepository(this WebApplicationBuilder builder,
        Action<ModuleRepositoryOption>? action = null)
    {
        return new ModuleRepositoryGuide().Register(action);
    }
    public static ModuleRepositoryGuide ConfigModuleRepository(this IServiceCollection services,
        Action<ModuleRepositoryOption>? action = null)
    {
        return new ModuleRepositoryGuide().Register(action);
    }
}