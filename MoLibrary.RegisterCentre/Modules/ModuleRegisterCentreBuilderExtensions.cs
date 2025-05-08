using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.RegisterCentre.Modules;

public static class ModuleRegisterCentreBuilderExtensions
{
    public static ModuleRegisterCentreGuide AddMoModuleRegisterCentre(this IServiceCollection services, Action<ModuleRegisterCentreOption>? action = null)
    {
        return new ModuleRegisterCentreGuide().Register(action);
    }
}