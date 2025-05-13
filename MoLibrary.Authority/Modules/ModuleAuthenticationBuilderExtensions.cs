using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Authority.Modules;

public static class ModuleAuthenticationBuilderExtensions
{
    public static ModuleAuthenticationGuide AddMoModuleAuthentication(this WebApplicationBuilder builder, Action<ModuleAuthenticationOption>? action = null)
    {
        return new ModuleAuthenticationGuide().Register(action);
    }
}