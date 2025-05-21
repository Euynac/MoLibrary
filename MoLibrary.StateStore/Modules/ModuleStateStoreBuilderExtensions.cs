using Microsoft.AspNetCore.Builder;

namespace MoLibrary.StateStore.Modules;


public static class ModuleStateStoreBuilderExtensions
{
    public static ModuleStateStoreGuide ConfigModuleStateStore(this WebApplicationBuilder builder,
        Action<ModuleStateStoreOption>? action = null)
    {
        return new ModuleStateStoreGuide().Register(action);
    }
}