using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Features.MoScopedData;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleScopedDataBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the ScopedData module.
        /// </summary>
        public static ModuleScopedDataGuide AddScopedData(Action<ModuleScopedDataOption>? action = null)
        {
            return new ModuleScopedDataGuide().Register(action);
        }
    }
}

/// <summary>
/// ScopedData module for managing contextual data within the scoped lifetime.
/// </summary>
public class ModuleScopedData(ModuleScopedDataOption option)
    : MoModule<ModuleScopedData, ModuleScopedDataOption, ModuleScopedDataGuide>(option)
{
    /// <summary>
    /// Configures service registrations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register the default scoped data provider with scoped lifetime.
        services.AddScoped<IMoScopedData, MoScopedDataDefaultScopedProvider>();
        
        base.ConfigureServices(services);
    }

    /// <summary>
    /// Gets the module key for the current module.
    /// </summary>
    /// <returns>The module key.</returns>
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.ScopedData;
    }
}

/// <summary>
/// Configuration guide for the ScopedData module.
/// </summary>
public class ModuleScopedDataGuide : MoModuleGuide<ModuleScopedData, ModuleScopedDataOption, ModuleScopedDataGuide>
{
    /// <summary>
    /// Registers a keyed scoped data service.
    /// </summary>
    /// <typeparam name="T">Scoped data implementation type. Must implement <see cref="IMoScopedData"/>.</typeparam>
    /// <param name="key">The service key.</param>
    /// <returns>The current guide instance.</returns>
    public ModuleScopedDataGuide AddKeyedScopedData<T>(string key) where T : class, IMoScopedData
    {
        ConfigureServices(context =>
            {
                context.Services.AddKeyedScoped<IMoScopedData, T>(key);
            }, secondKey: key);

        RecordKeyedServiceKey(key);
        return this;
    }

}

/// <summary>
/// Configuration options for the ScopedData module.
/// </summary>
public class ModuleScopedDataOption : MoModuleOption<ModuleScopedData>
{
    // Add module-specific options here when needed.
}
