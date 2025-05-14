using Microsoft.Extensions.DependencyInjection;
using System;

namespace MoLibrary.DependencyInjection.Modules;


public static class ModuleDynamicProxyBuilderExtensions
{
    public static ModuleDynamicProxyGuide AddMoModuleDynamicProxy(this IServiceCollection services,
        Action<ModuleDynamicProxyOption>? action = null)
    {
        return new ModuleDynamicProxyGuide().Register(action);
    }
}