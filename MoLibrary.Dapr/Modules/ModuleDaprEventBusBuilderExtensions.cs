using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Dapr.Modules;


public static class ModuleDaprEventBusBuilderExtensions
{
    public static ModuleDaprEventBusGuide AddMoModuleDaprEventBus(this IServiceCollection services,
        Action<ModuleDaprEventBusOption>? action = null)
    {
        return new ModuleDaprEventBusGuide().Register(action);
    }
}