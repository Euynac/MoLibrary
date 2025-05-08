using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Locker.Modules;


public static class ModuleLockerBuilderExtensions
{
    public static ModuleLockerGuide AddMoModuleLocker(this IServiceCollection services,
        Action<ModuleLockerOption>? action = null)
    {
        return new ModuleLockerGuide().Register(action);
    }
}