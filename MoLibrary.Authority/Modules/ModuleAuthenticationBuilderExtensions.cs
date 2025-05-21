using Microsoft.AspNetCore.Builder;

namespace MoLibrary.Authority.Modules;

public static class ModuleAuthenticationBuilderExtensions
{
    public static ModuleAuthenticationGuide ConfigModuleAuthentication(this WebApplicationBuilder builder, Action<ModuleAuthenticationOption>? action = null)
    {
        return new ModuleAuthenticationGuide().Register(action);
    }
}