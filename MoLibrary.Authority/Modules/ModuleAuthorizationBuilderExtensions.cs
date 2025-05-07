using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Authority.Modules;

public static class ModuleAuthorizationBuilderExtensions
{
    public static ModuleAuthorizationGuide AddMoModuleAuthorization<TEnum>(this IServiceCollection services, string claimTypeDefinition) where TEnum : struct, Enum
    {
        return new ModuleAuthorizationGuide().Register()
            .AddDefaultPermissionBit<TEnum>(claimTypeDefinition);
    }
}