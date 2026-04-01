using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DependencyInjection.AppInterfaces;
using Monica.DependencyInjection.DependencyInjection.Abstractions.Internal;
using Monica.DependencyInjection.DependencyInjection.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDependencyInjectionBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Enables Monica conventional dependency registration and cached service-provider access.
        /// </summary>
        public static ModuleDependencyInjectionGuide AddDependencyInjection(Action<ModuleDependencyInjectionOption>? action = null)
        {
            return new ModuleDependencyInjectionGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.DependencyInjection)]
public class ModuleDependencyInjection(ModuleDependencyInjectionOption option)
    : MoModule<ModuleDependencyInjection, ModuleDependencyInjectionOption, ModuleDependencyInjectionGuide>(option), IWantIterateBusinessTypes
{
    private IConventionalRegistrar? _registrar;
    private IServiceCollection? _services;

    public override void ConfigureServices(IServiceCollection services)
    {
        _registrar = new ConventionalRegistrar(Option);
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

/// <summary>
/// Configures the Monica dependency-injection module.
/// </summary>
public class ModuleDependencyInjectionGuide : MoModuleGuide<ModuleDependencyInjection, ModuleDependencyInjectionOption,
    ModuleDependencyInjectionGuide>
{
}

/// <summary>
/// Configures Monica conventional dependency registration behavior.
/// </summary>
public class ModuleDependencyInjectionOption : MoModuleOption<ModuleDependencyInjection>
{
    /// <summary>
    /// Gets or sets a value indicating whether the module should emit diagnostic logs for automatic service registration.
    /// </summary>
    /// <remarks>
    /// Enable this when you want to inspect how Monica discovers service lifetimes and exposed service types.
    /// The default is <c>false</c> to keep startup logging quiet.
    /// </remarks>
    public bool EnableAutoRegistrationLogging { get; set; }
}
