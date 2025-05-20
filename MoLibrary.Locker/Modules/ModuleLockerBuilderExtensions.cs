using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Locker.Modules;


public static class ModuleLockerBuilderExtensions
{
    public static ModuleLockerGuide ConfigModuleLocker(this WebApplicationBuilder builder,
        Action<ModuleLockerOption>? action = null)
    {
        return new ModuleLockerGuide().Register(action);
    }
}