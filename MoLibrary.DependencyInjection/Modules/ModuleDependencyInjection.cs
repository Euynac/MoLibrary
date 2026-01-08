using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.DependencyInjection.CoreInterfaces;
using MoLibrary.DependencyInjection.Implements;

namespace MoLibrary.DependencyInjection.Modules;

public static class ModuleDependencyInjectionBuilderExtensions
{
    public static ModuleDependencyInjectionGuide ConfigModuleDependencyInjection(this WebApplicationBuilder builder,
        Action<ModuleDependencyInjectionOption>? action = null)
    {
        return new ModuleDependencyInjectionGuide().Register(action);
    }
}

public class ModuleDependencyInjection(ModuleDependencyInjectionOption option)
    : MoModule<ModuleDependencyInjection, ModuleDependencyInjectionOption, ModuleDependencyInjectionGuide>(option), IWantIterateBusinessTypes
{
    private IConventionalRegistrar? _registrar;
    private IServiceCollection? _services;
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DependencyInjection;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        _registrar = new DefaultConventionalRegistrar(Option);
        services.AddTransient<IMoServiceProvider, DefaultMoServiceProvider>();
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
    /// <summary>
    /// 相关项目单元所在程序集名，使用名称包含查找。如若不配置，则默认仅扫描Entry程序集。
    /// </summary>
    public string[]? RelatedAssemblies { get; set; }
    public bool EnableDebug { get; set; }
}
