using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.DependencyInjection.AppInterfaces;
using Monica.DependencyInjection.CoreInterfaces;
using Monica.DependencyInjection.Implements;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDependencyInjectionBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 DependencyInjection 模块
        /// </summary>
        public static ModuleDependencyInjectionGuide AddDependencyInjection(Action<ModuleDependencyInjectionOption>? action = null)
        {
            return new ModuleDependencyInjectionGuide().Register(action);
        }
    }
}

public class ModuleDependencyInjection(ModuleDependencyInjectionOption option)
    : MoModule<ModuleDependencyInjection, ModuleDependencyInjectionOption, ModuleDependencyInjectionGuide>(option), IWantIterateBusinessTypes
{
    private IConventionalRegistrar? _registrar;
    private IServiceCollection? _services;
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.DependencyInjection;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        _registrar = new DefaultConventionalRegistrar(Option);
        services.AddScoped<ICachedServiceProvider, CachedServiceProvider>();
        _services = services;
    }


    /// <summary>
    /// Iterates through business types and registers them with the dependency injection container.
    /// </summary>
    /// <param name="types">The collection of types to iterate through.</param>
    /// <returns>An enumerable collection of the processed types.</returns>
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        if (_registrar == null || _services == null)
        {
            foreach (var type in types)
            {
                yield return type;
            }
            yield break;
        }
        
        foreach (var type in types)
        {
            _registrar.AddType(_services, type);
            yield return type;
        }
    }
}

public class ModuleDependencyInjectionGuide : MoModuleGuide<ModuleDependencyInjection, ModuleDependencyInjectionOption,
    ModuleDependencyInjectionGuide>
{
  
}

public class ModuleDependencyInjectionOption : MoModuleOption<ModuleDependencyInjection>
{
    public bool EnableDebug { get; set; }
}
