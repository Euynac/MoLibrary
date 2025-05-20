using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.RegisterCentre.Modules;

public static class ModuleRegisterCentreBuilderExtensions
{
    public static ModuleRegisterCentreGuide ConfigModuleRegisterCentre(this WebApplicationBuilder builder, Action<ModuleRegisterCentreOption>? action = null)
    {
        return new ModuleRegisterCentreGuide().Register(action);
    }
}