using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Repository.Modules;


public static class ModuleRepositoryBuilderExtensions
{
    public static ModuleRepositoryGuide AddMoModuleRepository(this IServiceCollection services,
        Action<ModuleRepositoryOption>? action = null)
    {
        return new ModuleRepositoryGuide().Register(action);
    }
}