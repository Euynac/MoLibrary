using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.SignalR.Modules;


public static class ModuleSignalRBuilderExtensions
{
    public static ModuleSignalRGuide ConfigModuleSignalR(this WebApplicationBuilder builder,
        Action<ModuleSignalROption>? action = null)
    {
        return new ModuleSignalRGuide().Register(action);
    }
}