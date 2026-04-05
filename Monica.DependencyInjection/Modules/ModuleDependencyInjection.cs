using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Abstractions.Internal;
using Monica.DependencyInjection.Services;

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

[ModuleKey(BuiltInModuleKey.DependencyInjection)]
public class ModuleDependencyInjection(ModuleDependencyInjectionOption option)
    : ModuleBase<ModuleDependencyInjection, ModuleDependencyInjectionOption, ModuleDependencyInjectionGuide>(option), IBusinessTypeIterator
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
public class ModuleDependencyInjectionGuide : ModuleGuide<ModuleDependencyInjection, ModuleDependencyInjectionOption,
    ModuleDependencyInjectionGuide>
{
}

/// <summary>
/// Configures Monica conventional dependency registration behavior.
/// </summary>
public class ModuleDependencyInjectionOption : ModuleOptions<ModuleDependencyInjection>
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
