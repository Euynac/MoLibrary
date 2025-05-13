using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.SignalR.Modules;


public static class ModuleSignalRBuilderExtensions
{
    public static ModuleSignalRGuide AddMoModuleSignalR(this WebApplicationBuilder builder,
        Action<ModuleSignalROption>? action = null)
    {
        return new ModuleSignalRGuide().Register(action);
    }
}