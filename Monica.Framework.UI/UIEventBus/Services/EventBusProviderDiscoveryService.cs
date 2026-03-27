using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Models;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Subscriptions;
using Monica.EventBus.Models;
using Monica.Modules;
using Monica.EventBus.Providers;
using Monica.Framework.UI.UIEventBus.Models;
using Monica.Tool.Results;

namespace Monica.Framework.UI.UIEventBus.Services;

/// <summary>
/// EventBus Provider Discovery Service - Provides Provider discovery and information query
/// </summary>
public class EventBusProviderDiscoveryService(
    IServiceProvider serviceProvider,
    ISubscriptionManager subscriptionManager,
    ILogger<EventBusProviderDiscoveryService> logger)
{
    /// <summary>
    /// Cached provider snapshots for efficient lookup
    /// </summary>
    private List<ModuleSnapshot>? _providerSnapshots;

    /// <summary>
    /// Gets or initializes the cached provider snapshots
    /// </summary>
    private List<ModuleSnapshot> ProviderSnapshots =>
        _providerSnapshots ??= MoModuleRegisterCentre.GetModuleProviders(EMoModuleKey.EventBus);

    #region Provider Discovery

    /// <summary>
    /// Get all registered EventBus Providers
    /// </summary>
    public Res<List<EventBusProviderInfo>> GetRegisteredProviders()
    {
        try
        {
            var providers = new List<EventBusProviderInfo>();

            // 1. Get the default Local EventBus
            var defaultLocalEventBus = serviceProvider.GetService<IMoLocalEventBus>();
            if (defaultLocalEventBus != null)
            {
                providers.Add(CreateLocalProviderInfo(null, defaultLocalEventBus));
            }

            // 2. Get the default Distributed EventBus (exclude NullDistributedEventBus)
            var defaultDistributedEventBus = serviceProvider.GetService<IMoDistributedEventBus>();
            if (defaultDistributedEventBus != null && defaultDistributedEventBus is not NullDistributedEventBus)
            {
                providers.Add(CreateDistributedProviderInfo(null, defaultDistributedEventBus));
            }

            // 3. Get the Keyed service key from the module registration
            var keyedServiceKeys = MoModuleRegisterCentre.GetKeyedServiceKeys(typeof(ModuleEventBus));

            // 4. Get Keyed Provider
            foreach (var key in keyedServiceKeys)
            {
                try
                {
                    // Try to get Keyed Local EventBus
                    var keyedLocalEventBus = serviceProvider.GetKeyedService<IMoLocalEventBus>(key);
                    if (keyedLocalEventBus != null)
                    {
                        providers.Add(CreateLocalProviderInfo(key, keyedLocalEventBus));
                    }

                    // Try to get Keyed Distributed EventBus
                    var keyedDistributedEventBus = serviceProvider.GetKeyedService<IMoDistributedEventBus>(key);
                    if (keyedDistributedEventBus != null && keyedDistributedEventBus is not NullDistributedEventBus)
                    {
                        providers.Add(CreateDistributedProviderInfo(key, keyedDistributedEventBus));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "解析 Keyed EventBus Provider 失败: {Key}", key);
                }
            }

            // 5. Get the Keyed service key of DaprEventBus
            var daprModuleType = ProviderSnapshots
                .FirstOrDefault(s => s.ModuleInstance is IEventBusModuleProvider { ProviderType: EEventBusProviderType.Dapr })
                ?.ModuleType;

            if (daprModuleType != null)
            {
                var daprKeyedServiceKeys = MoModuleRegisterCentre.GetKeyedServiceKeys(daprModuleType);
                foreach (var key in daprKeyedServiceKeys)
                {
                    try
                    {
                        var keyedDaprEventBus = serviceProvider.GetKeyedService<IMoDistributedEventBus>(key);
                        if (keyedDaprEventBus != null && keyedDaprEventBus is not NullDistributedEventBus)
                        {
                            // Check if the Provider already exists
                            if (providers.Any(p => p.ServiceKey == key && p.IsDistributed))
                                continue;

                            providers.Add(CreateDistributedProviderInfo(key, keyedDaprEventBus));
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "解析 Keyed Dapr EventBus Provider 失败: {Key}", key);
                    }
                }
            }

            // 6. Populate subscription statistics
            PopulateSubscriptionCounts(providers);

            return Res.Ok(providers);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取已注册 EventBus Provider 失败");
            return Res.Fail($"获取已注册 Provider 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Get Local Provider based on service key
    /// </summary>
    public Res<IMoLocalEventBus> GetLocalProvider(string? serviceKey)
    {
        try
        {
            IMoLocalEventBus? provider = serviceKey == null
                ? serviceProvider.GetService<IMoLocalEventBus>()
                : serviceProvider.GetKeyedService<IMoLocalEventBus>(serviceKey);

            if (provider == null)
            {
                return Res.Fail($"未找到 Local Provider: {serviceKey ?? "默认"}");
            }

            return Res.Ok(provider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取 Local Provider 失败: {Key}", serviceKey);
            return Res.Fail($"获取 Provider 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Get Distributed Provider based on service key
    /// </summary>
    public Res<IMoDistributedEventBus> GetDistributedProvider(string? serviceKey)
    {
        try
        {
            IMoDistributedEventBus? provider = serviceKey == null
                ? serviceProvider.GetService<IMoDistributedEventBus>()
                : serviceProvider.GetKeyedService<IMoDistributedEventBus>(serviceKey);

            if (provider == null || provider is NullDistributedEventBus)
            {
                return Res.Fail($"未找到 Distributed Provider: {serviceKey ?? "默认"}");
            }

            return Res.Ok(provider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取 Distributed Provider 失败: {Key}", serviceKey);
            return Res.Fail($"获取 Provider 失败: {ex.Message}");
        }
    }

    #endregion

    #region Private Methods

    private EventBusProviderInfo CreateLocalProviderInfo(string? serviceKey, IMoLocalEventBus provider)
    {
        return new EventBusProviderInfo
        {
            ServiceKey = serviceKey,
            ProviderType = EEventBusProviderType.Local,
            Capabilities = EEventBusCapabilities.None,
            IsDistributed = false,
            OptionType = typeof(ModuleEventBusOption),
            OptionInstance = GetLocalProviderOptionInfo(serviceKey),
            ImplementationType = provider.GetType().Name
        };
    }

    private EventBusProviderInfo CreateDistributedProviderInfo(string? serviceKey, IMoDistributedEventBus provider)
    {
        var (providerType, capabilities, displayName) = GetDistributedProviderMetadata(provider);
        var (optionType, optionInstance) = GetDistributedProviderOptionInfo(serviceKey, provider);

        return new EventBusProviderInfo
        {
            ServiceKey = serviceKey,
            ProviderType = providerType,
            Capabilities = capabilities,
            IsDistributed = true,
            OptionType = optionType,
            OptionInstance = optionInstance,
            ImplementationType = provider.GetType().Name
        };
    }

    /// <summary>
    /// Gets provider metadata from the registered IEventBusModuleProvider
    /// </summary>
    private (EEventBusProviderType providerType, EEventBusCapabilities capabilities, string displayName) GetDistributedProviderMetadata(IMoDistributedEventBus provider)
    {
        var providerTypeName = provider.GetType().FullName ?? "";

        foreach (var snapshot in ProviderSnapshots)
        {
            if (snapshot.ModuleInstance is not IEventBusModuleProvider moduleProvider) continue;

            // Match by checking if the provider type name contains the module's display name
            if (providerTypeName.Contains(moduleProvider.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return (moduleProvider.ProviderType, moduleProvider.Capabilities, moduleProvider.DisplayName);
            }
        }

        return (EEventBusProviderType.Unknown, EEventBusCapabilities.None, "Unknown");
    }

    /// <summary>
    /// Gets local provider option info
    /// </summary>
    private object? GetLocalProviderOptionInfo(string? serviceKey)
    {
        try
        {
            // Find a snapshot of ModuleEventBus
            var eventBusSnapshot = MoModuleRegisterCentre.ModuleSnapshots
                .FirstOrDefault(s => s.ModuleType == typeof(ModuleEventBus));

            if (eventBusSnapshot == null) return null;

            var (_, optionInstance) = eventBusSnapshot.GetKeyedOption(serviceProvider, serviceKey);
            return optionInstance;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "获取 Local Provider 配置失败: {ServiceKey}", serviceKey);
            return null;
        }
    }

    /// <summary>
    /// Gets distributed provider option information using ModuleSnapshot's generic option retrieval
    /// </summary>
    private (Type? optionType, object? optionInstance) GetDistributedProviderOptionInfo(string? serviceKey, IMoDistributedEventBus provider)
    {
        try
        {
            var providerTypeName = provider.GetType().FullName ?? "";

            // Find the matching provider module snapshot
            foreach (var snapshot in ProviderSnapshots)
            {
                if (snapshot.ModuleInstance is not IEventBusModuleProvider moduleProvider) continue;

                // Match by checking if the provider type name contains the module's display name
                if (providerTypeName.Contains(moduleProvider.DisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    // Use ModuleSnapshot's generic GetKeyedOption method
                    var (optionType, optionInstance) = snapshot.GetKeyedOption(serviceProvider, serviceKey);
                    return (optionType, optionInstance);
                }
            }

            return (null, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "获取 Distributed Provider 配置失败: {ServiceKey}", serviceKey);
            return (null, null);
        }
    }

    /// <summary>
    /// Populate subscription statistics for the Provider list
    /// </summary>
    private void PopulateSubscriptionCounts(List<EventBusProviderInfo> providers)
    {
        var allSubscriptions = subscriptionManager.GetAll().ToList();

        foreach (var provider in providers)
        {
            var matchingSubscriptions = allSubscriptions.Where(s =>
            {
                // Match ServiceKey
                var keyMatches = s.ServiceKey == provider.ServiceKey;

                // Match Scope
                var scopeMatches = provider.IsDistributed
                    ? s.Scope == SubscriptionScope.Distributed
                    : s.Scope == SubscriptionScope.Local;

                return keyMatches && scopeMatches;
            }).ToList();

            provider.SubscriptionCount = matchingSubscriptions.Count;
            provider.ActiveSubscriptionCount = matchingSubscriptions.Count(s => s.State == SubscriptionState.Active);
        }
    }

    #endregion
}
