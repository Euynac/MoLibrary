using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Authority.Modules;

public static class ModuleAuthorizationBuilderExtensions
{
    public static ModuleAuthorizationGuide AddMoModuleAuthorization<TEnum>(this WebApplicationBuilder builder, string claimTypeDefinition) where TEnum : struct, Enum
    {
        return new ModuleAuthorizationGuide().Register()
            .AddDefaultPermissionBit<TEnum>(claimTypeDefinition);
    }
}