using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services;
using Monica.Core.Results;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Models;
using Monica.EventBus.Providers.NoOp;
using Monica.Framework.UI.Localization;
using Monica.Framework.UI.UIEventBus.Models;
using Monica.Framework.UI.UIEventBus.State;
using Monica.Modules;

namespace Monica.Framework.UI.UIEventBus.Support;

/// <summary>
/// EventBus Provider Discovery Service - Provides Provider discovery and information query
/// </summary>
public class EventBusProviderDiscoveryService(
    IServiceProvider serviceProvider,
    MonicaApplication application,
    IEventSubscriptionRegistry subscriptionManager,
    IStringLocalizer<EventBusResource> localizer,
    ILogger<EventBusProviderDiscoveryService> logger)
{
    /// <summary>
    /// Cached provider snapshots for efficient lookup
    /// </summary>
    private IReadOnlyList<ModuleRuntimeSnapshot>? _providerSnapshots;
    private ModuleRuntimeSnapshot? _eventBusSnapshot;

    /// <summary>
    /// Gets or initializes the cached provider snapshots
    /// </summary>
    private IReadOnlyList<ModuleRuntimeSnapshot> ProviderSnapshots =>
        _providerSnapshots ??= application.Modules.GetModuleProviders(typeof(ModuleEventBus));

    private ModuleRuntimeSnapshot? EventBusSnapshot =>
        _eventBusSnapshot ??= application.Modules.RuntimeSnapshots.FirstOrDefault(snapshot =>
            snapshot.ModuleType == typeof(ModuleEventBus));

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
            var defaultLocalEventBus = serviceProvider.GetService<ILocalEventBus>();
            if (defaultLocalEventBus != null)
            {
                providers.Add(CreateLocalProviderInfo(null, defaultLocalEventBus));
            }

            // 2. Get the default Distributed EventBus (exclude NoOpDistributedEventBus)
            var defaultDistributedEventBus = serviceProvider.GetService<IDistributedEventBus>();
            if (defaultDistributedEventBus != null && defaultDistributedEventBus is not NoOpDistributedEventBus)
            {
                providers.Add(CreateDistributedProviderInfo(null, defaultDistributedEventBus));
            }

            // 3. Get the Keyed service key from the module registration
            var keyedServiceKeys = application.Modules.GetKeyedServiceKeys(typeof(ModuleEventBus));

            // 4. Get Keyed Provider
            foreach (var key in keyedServiceKeys)
            {
                try
                {
                    // Try to get Keyed Local EventBus
                    var keyedLocalEventBus = serviceProvider.GetKeyedService<ILocalEventBus>(key);
                    if (keyedLocalEventBus != null)
                    {
                        providers.Add(CreateLocalProviderInfo(key, keyedLocalEventBus));
                    }

                    // Try to get Keyed Distributed EventBus
                    var keyedDistributedEventBus = serviceProvider.GetKeyedService<IDistributedEventBus>(key);
                    if (keyedDistributedEventBus != null && keyedDistributedEventBus is not NoOpDistributedEventBus)
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
                .FirstOrDefault(s => s.ModuleInstance is IEventBusProviderModule { ProviderType: EventBusProviderKind.Dapr })
                ?.ModuleType;

            if (daprModuleType != null)
            {
                var daprKeyedServiceKeys = application.Modules.GetKeyedServiceKeys(daprModuleType);
                foreach (var key in daprKeyedServiceKeys)
                {
                    try
                    {
                        var keyedDaprEventBus = serviceProvider.GetKeyedService<IDistributedEventBus>(key);
                        if (keyedDaprEventBus != null && keyedDaprEventBus is not NoOpDistributedEventBus)
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
            return Res.Fail(localizer["Services:Providers:GetRegisteredFailed", ex.GetMessageRecursively()]);
        }
    }

    /// <summary>
    /// Get Local Provider based on service key
    /// </summary>
    public Res<ILocalEventBus> GetLocalProvider(string? serviceKey)
    {
        try
        {
            ILocalEventBus? provider = serviceKey == null
                ? serviceProvider.GetService<ILocalEventBus>()
                : serviceProvider.GetKeyedService<ILocalEventBus>(serviceKey);

            if (provider == null)
            {
                return Res.Fail(localizer[
                    "Services:Providers:LocalNotFound",
                    serviceKey ?? localizer["Services:Common:DefaultProviderKey"].Value]);
            }

            return Res.Ok(provider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取 Local Provider 失败: {Key}", serviceKey);
            return Res.Fail(localizer["Services:Providers:GetProviderFailed", ex.GetMessageRecursively()]);
        }
    }

    /// <summary>
    /// Get Distributed Provider based on service key
    /// </summary>
    public Res<IDistributedEventBus> GetDistributedProvider(string? serviceKey)
    {
        try
        {
            IDistributedEventBus? provider = serviceKey == null
                ? serviceProvider.GetService<IDistributedEventBus>()
                : serviceProvider.GetKeyedService<IDistributedEventBus>(serviceKey);

            if (provider == null || provider is NoOpDistributedEventBus)
            {
                return Res.Fail(localizer[
                    "Services:Providers:DistributedNotFound",
                    serviceKey ?? localizer["Services:Common:DefaultProviderKey"].Value]);
            }

            return Res.Ok(provider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取 Distributed Provider 失败: {Key}", serviceKey);
            return Res.Fail(localizer["Services:Providers:GetProviderFailed", ex.GetMessageRecursively()]);
        }
    }

    #endregion

    #region Private Methods

    private EventBusProviderInfo CreateLocalProviderInfo(string? serviceKey, ILocalEventBus provider)
    {
        return new EventBusProviderInfo
        {
            ServiceKey = serviceKey,
            ProviderType = EventBusProviderKind.Local,
            Capabilities = EventBusProviderCapabilities.None,
            IsDistributed = false,
            ImplementationType = provider.GetType().Name,
            OptionDiagnosticsTarget = EventBusSnapshot is { } snapshot
                ? new ModuleOptionDiagnosticsTarget(
                    snapshot.ModuleKey,
                    ModuleOptionProfileSelector.Default)
                : null
        };
    }

    private EventBusProviderInfo CreateDistributedProviderInfo(string? serviceKey, IDistributedEventBus provider)
    {
        var (providerType, capabilities, providerSnapshot) = GetDistributedProviderMetadata(provider);

        return new EventBusProviderInfo
        {
            ServiceKey = serviceKey,
            ProviderType = providerType,
            Capabilities = capabilities,
            IsDistributed = true,
            ImplementationType = provider.GetType().Name,
            OptionDiagnosticsTarget = providerSnapshot is null
                ? null
                : new ModuleOptionDiagnosticsTarget(
                    providerSnapshot.ModuleKey,
                    serviceKey is null
                        ? ModuleOptionProfileSelector.Default
                        : ModuleOptionProfileSelector.NamedOrDefault(serviceKey))
        };
    }

    /// <summary>
    /// Gets provider metadata from the registered IEventBusProviderModule
    /// </summary>
    private (
        EventBusProviderKind ProviderType,
        EventBusProviderCapabilities Capabilities,
        ModuleRuntimeSnapshot? ProviderSnapshot) GetDistributedProviderMetadata(
        IDistributedEventBus provider)
    {
        var providerTypeName = provider.GetType().FullName ?? "";

        foreach (var snapshot in ProviderSnapshots)
        {
            if (snapshot.ModuleInstance is not IEventBusProviderModule moduleProvider) continue;

            // Match by checking if the provider type name contains the module's display name
            if (providerTypeName.Contains(moduleProvider.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return (moduleProvider.ProviderType, moduleProvider.Capabilities, snapshot);
            }
        }

        return (EventBusProviderKind.Unknown, EventBusProviderCapabilities.None, null);
    }

    /// <summary>
    /// Populate subscription statistics for the Provider list
    /// </summary>
    private void PopulateSubscriptionCounts(List<EventBusProviderInfo> providers)
    {
        var allSubscriptions = subscriptionManager.GetAll()
            .Where(subscription => !EventBusTestMetadataKeys.IsTestListenerSubscription(subscription))
            .ToList();

        foreach (var provider in providers)
        {
            var matchingSubscriptions = allSubscriptions.Where(s =>
            {
                // Match ServiceKey
                var keyMatches = s.ServiceKey == provider.ServiceKey;

                // Match Scope
                var scopeMatches = provider.IsDistributed
                    ? s.Scope == EventSubscriptionScope.Distributed
                    : s.Scope == EventSubscriptionScope.Local;

                return keyMatches && scopeMatches;
            }).ToList();

            provider.SubscriptionCount = matchingSubscriptions.Count;
            provider.ActiveSubscriptionCount = matchingSubscriptions.Count(s => s.State == EventSubscriptionState.Active);
        }
    }

    #endregion
}
