using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.DependencyInjection.CoreInterfaces;
using MoLibrary.DependencyInjection.Implements;

namespace MoLibrary.DependencyInjection.Modules;

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