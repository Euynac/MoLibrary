using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.EventBus.Modules;


public static class ModuleEventBusBuilderExtensions
{
    public static ModuleEventBusGuide ConfigModuleEventBus(this IServiceCollection services,
        Action<ModuleEventBusOption>? action = null)
    {
        return new ModuleEventBusGuide().Register(action);
    }
}