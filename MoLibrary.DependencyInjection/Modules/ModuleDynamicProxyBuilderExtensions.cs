using Microsoft.AspNetCore.Builder;

namespace MoLibrary.DependencyInjection.Modules;


public static class ModuleDynamicProxyBuilderExtensions
{
    public static ModuleDynamicProxyGuide ConfigModuleDynamicProxy(this WebApplicationBuilder builder,
        Action<ModuleDynamicProxyOption>? action = null)
    {
        return new ModuleDynamicProxyGuide().Register(action).ConfigDynamicProxyServices();
    }
}