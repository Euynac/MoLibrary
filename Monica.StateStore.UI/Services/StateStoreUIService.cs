using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Modules;
using Monica.StateStore.UI.Models;
using Monica.StateStore.UI.Services.Browser;
using Monica.Core.Results;
using Monica.StateStore.Abstractions;
using Monica.StateStore.UI.Localization;

namespace Monica.StateStore.UI.Services;

/// <summary>
/// UI service for state store provider discovery and dashboard operations.
/// </summary>
public class StateStoreUIService(
    IServiceProvider serviceProvider,
    MonicaApplication application,
    IOptions<ModuleStateStoreOption> stateStoreOption,
    IEnumerable<IStateStoreBrowserApi> browserApis,
    IStringLocalizer<StateStoreResource> localizer,
    ILogger<StateStoreUIService> logger)
{
    private readonly IReadOnlyList<IStateStoreBrowserApi> _browserApis = browserApis.ToList();
    private IReadOnlyList<ModuleRuntimeSnapshot>? _providerSnapshots;

    private IReadOnlyList<ModuleRuntimeSnapshot> ProviderSnapshots =>
        _providerSnapshots ??= application.Modules.GetModuleProviders(typeof(ModuleStateStore));

    #region Provider Discovery

    public Res<List<StateStoreProviderInfo>> GetRegisteredProviders()
    {
        try
        {
            var providers = new List<StateStoreProviderInfo>();
            var keyedServiceKeys = application.Modules.GetKeyedServiceKeys(typeof(ModuleStateStore));

            var defaultProvider = serviceProvider.GetService<IStateStore>();
            if (defaultProvider != null)
            {
                providers.Add(CreateProviderInfo(null, defaultProvider));
            }

            foreach (var key in keyedServiceKeys)
            {
                try
                {
                    var keyedProvider = serviceProvider.GetKeyedService<IStateStore>(key);
                    if (keyedProvider != null)
                    {
                        providers.Add(CreateProviderInfo(key, keyedProvider));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to resolve keyed state store provider: {Key}", key);
                }
            }

            return Res.Ok(providers
                .OrderByDescending(provider => provider.IsDefaultStateStore)
                .ThenBy(provider => provider.ServiceKey ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get registered state store providers.");
            return Res.Fail(BuildDetailedErrorMessage("Service:Errors:ProviderList", ex));
        }
    }

    public Res<IStateStore> GetProvider(string? serviceKey)
    {
        try
        {
            IStateStore? provider = serviceKey == null
                ? serviceProvider.GetService<IStateStore>()
                : serviceProvider.GetKeyedService<IStateStore>(serviceKey);

            if (provider == null)
            {
                return Res.Fail(localizer["Service:Errors:ProviderNotFound", serviceKey ?? localizer["Service:Labels:DefaultProvider"]]);
            }

            return Res.Ok(provider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get state store provider: {ServiceKey}", serviceKey);
            return Res.Fail(BuildDetailedErrorMessage("Service:Errors:ProviderLoad", ex));
        }
    }

    private StateStoreProviderInfo CreateProviderInfo(string? serviceKey, IStateStore provider)
    {
        var (providerType, capabilities, providerSnapshot) = GetProviderMetadata(provider);
        var browserApi = GetBrowserApi(providerType, provider);
        var browserFeatures = browserApi.GetFeatures(provider);
        var defaultSearchMode = browserApi.GetDefaultSearchMode(provider);

        var isDefaultStateStore = false;
        if (serviceKey == null)
        {
            var useDistributed = stateStoreOption.Value.UseDistributedProviderAsDefault;
            var isDistributed = provider is IDistributedStateStore;
            isDefaultStateStore = useDistributed == isDistributed;
        }

        return new StateStoreProviderInfo
        {
            ServiceKey = serviceKey,
            ProviderType = providerType,
            Capabilities = capabilities,
            BrowserFeatures = browserFeatures,
            DefaultSearchMode = defaultSearchMode,
            IsDistributed = provider is IDistributedStateStore,
            IsDefaultStateStore = isDefaultStateStore,
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

    private (
        EStateStoreProviderType ProviderType,
        EStateStoreCapabilities Capabilities,
        ModuleRuntimeSnapshot? ProviderSnapshot) GetProviderMetadata(IStateStore provider)
    {
        var providerTypeName = provider.GetType().FullName ?? string.Empty;

        foreach (var snapshot in ProviderSnapshots)
        {
            if (snapshot.ModuleInstance is not IStateStoreModuleProvider moduleProvider)
            {
                continue;
            }

            if (providerTypeName.Contains(moduleProvider.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    moduleProvider.ProviderType,
                    moduleProvider.Capabilities,
                    snapshot);
            }
        }

        if (provider is IMemoryStateStore)
        {
            return (EStateStoreProviderType.Memory,
                EStateStoreCapabilities.KeyScanning | EStateStoreCapabilities.BulkOperations,
                null);
        }

        return (EStateStoreProviderType.Unknown, EStateStoreCapabilities.BulkOperations, null);
    }

    private IStateStoreBrowserApi GetBrowserApi(EStateStoreProviderType providerType, IStateStore provider)
    {
        return _browserApis.First(api => api.CanHandle(providerType, provider));
    }

    #endregion

    #region Key Operations

    public async Task<Res<KeyScanResult>> ScanKeysAsync(
        string? serviceKey,
        string pattern = "*",
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var browseResult = await SearchKeysAsync(serviceKey, new StateStoreKeyBrowseRequest
        {
            Query = pattern,
            Limit = limit,
            SearchMode = EStateStoreKeySearchMode.PatternScan
        }, cancellationToken);

        if (browseResult.IsFailed(out var error, out var data))
        {
            return Res.Fail(error);
        }

        return Res.Ok(new KeyScanResult
        {
            Keys = data.Items.Select(item => item.Key).ToList(),
            TotalCount = data.TotalCount,
            HasMore = data.HasMore
        });
    }

    public async Task<Res<StateStoreKeyBrowseResult>> SearchKeysAsync(
        string? serviceKey,
        StateStoreKeyBrowseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (GetProvider(serviceKey).IsFailed(out var error, out var provider))
        {
            return Res.Fail(error);
        }

        try
        {
            var (providerType, _, _) = GetProviderMetadata(provider);
            var browserApi = GetBrowserApi(providerType, provider);
            return Res.Ok(await browserApi.BrowseAsync(provider, request, cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search keys for query: {Query}", request.Query);
            return Res.Fail(BuildDetailedErrorMessage("Service:Errors:KeySearch", ex));
        }
    }

    public async Task<Res<StateStoreKeyInfo>> GetKeyAsync(
        string? serviceKey,
        string key,
        CancellationToken cancellationToken = default)
    {
        if (GetProvider(serviceKey).IsFailed(out var error, out var provider))
        {
            return Res.Fail(error);
        }

        try
        {
            var (providerType, _, _) = GetProviderMetadata(provider);
            var browserApi = GetBrowserApi(providerType, provider);
            return Res.Ok(await browserApi.LoadKeyAsync(provider, key, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return Res.Fail(localizer["Service:Errors:KeyNotFound", key]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load state store key: {Key}", key);
            return Res.Fail(BuildDetailedErrorMessage("Service:Errors:KeyLoad", ex));
        }
    }

    public async Task<Res<bool>> KeyExistsAsync(
        string? serviceKey,
        string key,
        CancellationToken cancellationToken = default)
    {
        if (GetProvider(serviceKey).IsFailed(out var error, out var provider))
        {
            return Res.Fail(error);
        }

        try
        {
            return Res.Ok(await provider.ExistAsync(key, cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check whether key exists: {Key}", key);
            return Res.Fail(BuildDetailedErrorMessage("Service:Errors:KeyCheck", ex));
        }
    }

    public async Task<Res> SaveKeyAsync(
        string? serviceKey,
        StateStoreKeyUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (GetProvider(serviceKey).IsFailed(out var error, out var provider))
        {
            return error;
        }

        try
        {
            var value = ParseRequestValue(request);

            if (!string.IsNullOrEmpty(request.ETag))
            {
                var (success, _) = await provider.TrySaveStateWithETagAsync(
                    request.Key,
                    value,
                    request.ETag,
                    cancellationToken,
                    request.TTL);

                if (!success)
                {
                    return Res.Fail(localizer["Service:Errors:ETagMismatch"]);
                }
            }
            else
            {
                await provider.SaveStateAsync(request.Key, value, cancellationToken, request.TTL);
            }

            return Res.Ok(localizer["Service:Success:KeySaved"]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save state store key: {Key}", request.Key);
            return Res.Fail(BuildDetailedErrorMessage("Service:Errors:KeySave", ex));
        }
    }

    public async Task<Res<StateStoreKeyInfo>> CreateKeyAsync(
        string? serviceKey,
        StateStoreKeyUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (GetProvider(serviceKey).IsFailed(out var error, out var provider))
        {
            return error;
        }

        try
        {
            var value = ParseRequestValue(request);
            var created = await provider.TrySaveStateIfNotExistsAsync(
                request.Key,
                value,
                cancellationToken,
                request.TTL);

            if (!created)
            {
                return Res.Fail(localizer["Service:Errors:KeyExists"]);
            }

            var createdKey = await GetKeyAsync(serviceKey, request.Key, cancellationToken);
            if (createdKey.IsOk(out var loadedKey))
            {
                return Res.Ok(loadedKey);
            }

            return Res.Ok(new StateStoreKeyInfo
            {
                Key = request.Key,
                RawValue = request.Value,
                IsValueLoaded = true
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create state store key: {Key}", request.Key);
            return Res.Fail(BuildDetailedErrorMessage("Service:Errors:KeyCreate", ex));
        }
    }

    public async Task<Res> DeleteKeyAsync(
        string? serviceKey,
        string key,
        string? etag = null,
        CancellationToken cancellationToken = default)
    {
        if (GetProvider(serviceKey).IsFailed(out var error, out var provider))
        {
            return error;
        }

        try
        {
            if (!string.IsNullOrEmpty(etag))
            {
                var success = await provider.TryDeleteStateWithETagAsync(key, etag, cancellationToken);
                if (!success)
                {
                    return Res.Fail(localizer["Service:Errors:ETagMismatch"]);
                }
            }
            else
            {
                await provider.DeleteStateAsync(key, cancellationToken);
            }

            return Res.Ok(localizer["Service:Success:KeyDeleted"]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete state store key: {Key}", key);
            return Res.Fail(BuildDetailedErrorMessage("Service:Errors:KeyDelete", ex));
        }
    }

    public async Task<Res> DeleteKeysAsync(
        string? serviceKey,
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default)
    {
        if (GetProvider(serviceKey).IsFailed(out var error, out var provider))
        {
            return error;
        }

        try
        {
            await provider.DeleteBulkStateAsync(keys, cancellationToken);
            return Res.Ok(localizer["Service:Success:KeysDeleted", keys.Count]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete state store keys in bulk.");
            return Res.Fail(BuildDetailedErrorMessage("Service:Errors:KeysDelete", ex));
        }
    }

    #endregion

    private string BuildDetailedErrorMessage(string operationKey, Exception ex)
    {
        return localizer["Service:Errors:OperationDetail", localizer[operationKey], ex.GetMessageRecursively()];
    }

    private static object? ParseRequestValue(StateStoreKeyUpdateRequest request)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<object>(request.Value);
        }
        catch
        {
            return request.Value;
        }
    }
}
