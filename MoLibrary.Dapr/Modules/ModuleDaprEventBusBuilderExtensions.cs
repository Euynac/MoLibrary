using Microsoft.AspNetCore.Builder;

namespace MoLibrary.Dapr.Modules;


public static class ModuleDaprEventBusBuilderExtensions
{
    public static ModuleDaprEventBusGuide ConfigModuleDaprEventBus(this WebApplicationBuilder builder,
        Action<ModuleDaprEventBusOption>? action = null)
    {
        return new ModuleDaprEventBusGuide().Register(action);
    }
}