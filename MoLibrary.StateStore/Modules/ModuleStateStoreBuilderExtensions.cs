using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.StateStore.Modules;


public static class ModuleStateStoreBuilderExtensions
{
    public static ModuleStateStoreGuide AddMoModuleStateStore(this IServiceCollection services,
        Action<ModuleStateStoreOption>? action = null)
    {
        return new ModuleStateStoreGuide().Register(action);
    }
}