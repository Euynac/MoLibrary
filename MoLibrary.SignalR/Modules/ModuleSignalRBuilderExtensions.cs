using Microsoft.AspNetCore.Builder;

namespace MoLibrary.SignalR.Modules;


public static class ModuleSignalRBuilderExtensions
{
    public static ModuleSignalRGuide ConfigModuleSignalR(this WebApplicationBuilder builder,
        Action<ModuleSignalROption>? action = null)
    {
        return new ModuleSignalRGuide().Register(action);
    }
}