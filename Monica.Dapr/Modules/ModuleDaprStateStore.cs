using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Dapr.Services;
using Monica.StateStore.Abstractions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDaprStateStoreBuilderExtensions
{
    /// <summary>
    /// Selects Dapr as the host's common distributed state store provider.
    /// </summary>
    /// <param name="module">The StateStore registration that will use Dapr.</param>
    /// <param name="action">Optional Dapr state-store configuration.</param>
    /// <param name="documentProfileName">The declared durable-document profile selected for this store.</param>
    /// <returns>The Dapr StateStore provider registration.</returns>
    public static ModuleRegistration<ModuleDaprStateStore, ModuleDaprStateStoreOption> UseDaprStateStoreProvider(
        this ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> module,
        Action<ModuleDaprStateStoreOption>? action = null,
        string documentProfileName = ModuleStateStoreOption.DURABLE_JSON_PROFILE)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentProfileName);
        module.Configure(options => EnsureDocumentProfileExists(options, documentProfileName));
        module.SetCommonDistributedStateStoreProvider<DaprStateStoreProvider>();
        return module.Include<ModuleDaprStateStore, ModuleDaprStateStoreOption>(options =>
        {
            action?.Invoke(options);
            options.DocumentProfileName = documentProfileName;
        });
    }
    
    /// <summary>
    /// Registers Dapr state store as a keyed state store provider.
    /// </summary>
    /// <param name="module">The StateStore registration being configured.</param>
    /// <param name="serviceKey">The service key that identifies this state store instance.</param>
    /// <param name="configureOptions">Delegate that configures the Dapr state store.</param>
    /// <param name="documentProfileName">The declared durable-document profile selected for this store.</param>
    /// <returns>The same StateStore module registration for chaining.</returns>
    public static ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> AddKeyedDaprStateStore(
        this ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> module,
        string serviceKey,
        Action<ModuleDaprStateStoreOption> configureOptions,
        string documentProfileName = ModuleStateStoreOption.DURABLE_JSON_PROFILE)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configureOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentProfileName);
        module.Configure(options => EnsureDocumentProfileExists(options, documentProfileName));
        var providerModule = module.Include<ModuleDaprStateStore, ModuleDaprStateStoreOption>()
            .ConfigureProfile(serviceKey, options =>
            {
                configureOptions(options);
                options.DocumentProfileName = documentProfileName;
            });
        providerModule.ConfigureServices(context =>
        {
            var options = Options.Create(providerModule.GetProfile(serviceKey));
            context.Services.AddKeyedSingleton<IStateStore>(serviceKey, (sp, _) =>
            {
                return ActivatorUtilities.CreateInstance<DaprStateStoreProvider>(sp, options);
            });
        });

        module.RecordKeyedServiceKey(serviceKey);
        return module;
    }

    private static void EnsureDocumentProfileExists(
        ModuleStateStoreOption options,
        string documentProfileName)
    {
        if (!options.ContainsDocumentProfile(documentProfileName))
        {
            throw new InvalidOperationException(
                $"State document profile '{documentProfileName}' must be declared before a Dapr store selects it.");
        }
    }
}

public class ModuleDaprStateStore : MonicaModule<ModuleDaprStateStoreOption>,
      IStateStoreModuleProvider
{

    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleDaprClient, ModuleDaprClientOption>();
        module.Require<ModuleStateStore, ModuleStateStoreOption>();
    }

    #region IStateStoreModuleProvider Implementation

    public Type ProvidesFor => typeof(ModuleStateStore);

    public EStateStoreProviderType ProviderType => EStateStoreProviderType.Dapr;

    public EStateStoreCapabilities Capabilities =>
        EStateStoreCapabilities.RawStringRetrieval |
        EStateStoreCapabilities.QueryState |
        EStateStoreCapabilities.BulkOperations;

    public string DisplayName => "Dapr";

    #endregion
}



public class ModuleDaprStateStoreOption : ModuleOptions<ModuleDaprStateStore>
{
    /// <summary>
    /// Gets the immutable state-document profile selected for every typed operation of this logical Dapr store.
    /// </summary>
    public string DocumentProfileName { get; internal set; } = ModuleStateStoreOption.DURABLE_JSON_PROFILE;
    /// <summary>
    /// Name of the Dapr state store. Must match the <c>name</c> field in the Dapr state store
    /// component YAML metadata.
    /// </summary>
    [Required]
    public string StateStoreName { get; set; } = null!;

    /// <summary>
    /// Number of concurrent get operations issued by the Dapr runtime to the state store. Values
    /// less than or equal to 0 remove the limit.
    /// </summary>
    public int? DefaultBulkParallelism { get; set; } 
}
