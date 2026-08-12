using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.StateStore.Abstractions;
using Monica.StateStore.Providers.Memory;
using Monica.StateStore.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleStateStoreBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the StateStore module
        /// </summary>
        public ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> AddStateStore(Action<ModuleStateStoreOption>? action = null)
        {
            return builder.AddModule<ModuleStateStore, ModuleStateStoreOption>(action);
        }
    }
}

public class ModuleStateStore : MonicaModule<ModuleStateStoreOption>
{
    private IStateDocumentProfileProvider? _documentProfileProvider;

    /// <summary>
    /// Identifies the distributed-provider capability required by features that resolve
    /// <see cref="IDistributedStateStore"/>.
    /// </summary>
    public const string DISTRIBUTED_PROVIDER_FEATURE = "distributed-provider";

    public override void ConfigureServices(ModuleContext<ModuleStateStoreOption> context)
    {
        var services = context.Services;
        services.AddMemoryCache();
        services.AddSingleton<IMemoryStateStore, MemoryCacheProvider>();
        services.AddSingleton<IStateDocumentProfileProvider>(
            _documentProfileProvider
            ?? throw new InvalidOperationException(
                "State document profiles are unavailable before module options have been finalized."));
        if (Option.UseDistributedProviderAsDefault)
        {
            services.AddSingleton<IStateStore>(serviceProvider =>
                serviceProvider.GetRequiredService<IDistributedStateStore>());
        }
        else
        {
            services.AddSingleton<IStateStore>(serviceProvider => serviceProvider.GetRequiredService<IMemoryStateStore>());
        }
    }

    /// <inheritdoc />
    public override void ValidateOptions(ModuleStateStoreOption options, string? profileName)
    {
        var profiles = options.BuildDocumentProfiles();
        if (profileName is null)
        {
            _documentProfileProvider = new StateDocumentProfileProvider(profiles);
        }
    }
}

public static class ModuleStateStoreRegistrationExtensions
{
    /// <summary>
    /// Register a common distributed state store provider
    /// </summary>
    /// <param name="module">The StateStore registration being configured.</param>
    /// <typeparam name="TProvider">Distributed state store provider type</typeparam>
    /// <returns>The current StateStore module registration for chaining.</returns>
    public static ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> SetCommonDistributedStateStoreProvider<TProvider>(this ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> module)
        where TProvider : class, IDistributedStateStore
    {
        module.RequireFeature(ModuleStateStore.DISTRIBUTED_PROVIDER_FEATURE);
        return module
            .Configure(options => options.UseDistributedProviderAsDefault = true)
            .ConfigureServices(context => context.Services.AddSingleton<IDistributedStateStore, TProvider>())
            .SatisfyFeature(ModuleStateStore.DISTRIBUTED_PROVIDER_FEATURE);
    }

    /// <summary>
    /// Configures the built-in durable JSON document profile for this host.
    /// </summary>
    /// <param name="module">The StateStore registration being configured.</param>
    /// <param name="configure">The serializer-options contribution.</param>
    /// <returns>The current StateStore registration for chaining.</returns>
    public static ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> ConfigureDurableJsonProfile(
        this ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> module,
        Action<JsonSerializerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return module.Configure(options => options.ConfigureDurableJsonProfile(configure));
    }

    /// <summary>
    /// Adds an explicitly named versioned JSON document profile for logical state stores.
    /// </summary>
    /// <param name="module">The StateStore registration being configured.</param>
    /// <param name="name">The case-sensitive profile name.</param>
    /// <param name="contractVersion">The application-managed persisted contract version.</param>
    /// <param name="configure">Optional serializer customization.</param>
    /// <returns>The current StateStore registration for chaining.</returns>
    public static ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> AddJsonDocumentProfile(
        this ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> module,
        string name,
        string contractVersion,
        Action<JsonSerializerOptions>? configure = null)
    {
        return module.Configure(options =>
            options.AddJsonDocumentProfile(name, contractVersion, configure));
    }

    /// <summary>
    /// Add a keyed state store using the common provider
    /// </summary>
    /// <param name="module">The StateStore registration being configured.</param>
    /// <param name="key">Service key</param>
    /// <param name="useDistributed">Whether to use distributed storage, false uses memory storage</param>
    /// <returns>The current StateStore module registration for chaining.</returns>
    public static ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> AddKeyedCommonStateStore(this ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> module, string key, bool useDistributed = false)
    {
        if (useDistributed)
        {
            module.RequireFeature(ModuleStateStore.DISTRIBUTED_PROVIDER_FEATURE);
        }

        module.ConfigureServices(context =>
        {
            if (useDistributed)
            {
                context.Services.TryAddKeyedSingleton<IStateStore>(key, (serviceProvider, _) =>
                    serviceProvider.GetRequiredService<IDistributedStateStore>());
            }
            else
            {
                context.Services.TryAddKeyedSingleton<IStateStore>(key, (serviceProvider, _) =>
                    serviceProvider.GetRequiredService<IMemoryStateStore>());
            }
        });

        module.RecordKeyedServiceKey(key);
        return module;
    }

    /// <summary>
    /// Add a keyed abstract state store provider
    /// </summary>
    /// <param name="module">The StateStore registration being configured.</param>
    /// <typeparam name="TProvider">State store provider type</typeparam>
    /// <param name="key">Service key</param>
    /// <returns>The current StateStore module registration for chaining.</returns>
    public static ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> AddKeyedStateStore<TProvider>(this ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> module, string key) where TProvider : class, IStateStore
    {
        module.ConfigureServices(context => context.Services.AddKeyedSingleton<IStateStore, TProvider>(key));
        module.RecordKeyedServiceKey(key);
        return module;
    }

    /// <summary>
    /// Configure custom StateStore service registration
    /// </summary>
    /// <param name="module">The StateStore registration being configured.</param>
    /// <param name="configureServices">Service configuration delegate</param>
    /// <param name="key">Optional key to differentiate multiple calls (used as secondKey in module system)</param>
    /// <returns>The current StateStore module registration for chaining.</returns>
    public static ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> ConfigureStateStoreServices(this ModuleRegistration<ModuleStateStore, ModuleStateStoreOption> module, Action<IServiceCollection> configureServices, string? key = null)
    {
        module.ConfigureServices(context =>
        {
            configureServices(context.Services);
        });
        return module;
    }

}

public class ModuleStateStoreOption : ModuleOptions<ModuleStateStore>
{
    /// <summary>
    /// Identifies the built-in versioned JSON profile intended for ordinary durable application state.
    /// </summary>
    public const string DURABLE_JSON_PROFILE = "durable-json";

    private const string DURABLE_JSON_CONTRACT_VERSION = "1";
    private readonly List<Action<JsonSerializerOptions>> _durableProfileContributions = [];
    private readonly Dictionary<string, DocumentProfileDefinition> _documentProfiles =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Use distributed state storage as the default (non-Keyed service) <see cref="IStateStore"/> implementation
    /// </summary>
    public bool UseDistributedProviderAsDefault { get; internal set; }

    /// <summary>
    /// Configures the built-in durable JSON profile used by providers unless another profile is selected.
    /// </summary>
    /// <param name="configure">A serializer-options contribution applied to the durable defaults.</param>
    /// <remarks>
    /// Changing this profile after data has been persisted can make existing documents incompatible. Treat changes as
    /// a state-schema migration. These settings never affect HTTP, MVC, SignalR, or Dapr invocation JSON.
    /// </remarks>
    public void ConfigureDurableJsonProfile(Action<JsonSerializerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _durableProfileContributions.Add(configure);
    }

    /// <summary>
    /// Adds one explicitly named, versioned durable JSON document profile.
    /// </summary>
    /// <param name="name">The case-sensitive logical profile name selected by store registrations.</param>
    /// <param name="contractVersion">The application-managed persisted contract version.</param>
    /// <param name="configure">Optional serializer customization applied after durable defaults.</param>
    /// <exception cref="InvalidOperationException">Thrown when the name is already defined.</exception>
    public void AddJsonDocumentProfile(
        string name,
        string contractVersion,
        Action<JsonSerializerOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractVersion);
        if (string.Equals(name, DURABLE_JSON_PROFILE, StringComparison.Ordinal)
            || !_documentProfiles.TryAdd(name, new DocumentProfileDefinition(contractVersion, configure)))
        {
            throw new InvalidOperationException($"State document profile '{name}' is already defined.");
        }
    }

    /// <summary>
    /// Determines whether a state-document profile with the supplied case-sensitive name is declared.
    /// </summary>
    /// <param name="name">The profile name to inspect.</param>
    /// <returns><see langword="true" /> when the built-in durable profile or a custom profile exists.</returns>
    public bool ContainsDocumentProfile(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return string.Equals(name, DURABLE_JSON_PROFILE, StringComparison.Ordinal)
               || _documentProfiles.ContainsKey(name);
    }

    internal IReadOnlyList<StateDocumentProfile> BuildDocumentProfiles()
    {
        var profiles = new List<StateDocumentProfile>(_documentProfiles.Count + 1)
        {
            StateDocumentProfile.CreateJson(
                DURABLE_JSON_PROFILE,
                DURABLE_JSON_CONTRACT_VERSION,
                options =>
                {
                    foreach (var contribution in _durableProfileContributions)
                    {
                        contribution(options);
                    }
                })
        };
        profiles.AddRange(_documentProfiles.Select(pair =>
            StateDocumentProfile.CreateJson(pair.Key, pair.Value.ContractVersion, pair.Value.Configure)));
        return profiles;
    }

    private sealed record DocumentProfileDefinition(
        string ContractVersion,
        Action<JsonSerializerOptions>? Configure);
}
