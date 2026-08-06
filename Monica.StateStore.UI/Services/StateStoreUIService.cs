using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services;
using Monica.Modules;
using Monica.StateStore.UI.Models;
using Monica.StateStore.UI.Services.Browser;
using Monica.Core.Results;
using Monica.StateStore.Abstractions;

namespace Monica.StateStore.UI.Services;

/// <summary>
/// UI service for state store provider discovery and dashboard operations.
/// </summary>
public class StateStoreUIService(
    IServiceProvider serviceProvider,
    MonicaApplication application,
    IOptions<ModuleStateStoreOption> stateStoreOption,
    IEnumerable<IStateStoreBrowserApi> browserApis,
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
                .ThenBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get registered state store providers.");
            return Res.Fail(BuildDetailedErrorMessage("获取已注册 Provider 失败", ex));
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
                return Res.Fail($"未找到 Provider: {serviceKey ?? "默认"}");
            }

            return Res.Ok(provider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get state store provider: {ServiceKey}", serviceKey);
            return Res.Fail(BuildDetailedErrorMessage("获取 Provider 失败", ex));
        }
    }

    private StateStoreProviderInfo CreateProviderInfo(string? serviceKey, IStateStore provider)
    {
        var (providerType, capabilities, providerDisplayName) = GetProviderMetadata(provider);
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
            ProviderDisplayName = providerDisplayName,
            ProviderType = providerType,
            Capabilities = capabilities,
            BrowserFeatures = browserFeatures,
            DefaultSearchMode = defaultSearchMode,
            IsDistributed = provider is IDistributedStateStore,
            IsDefaultStateStore = isDefaultStateStore,
            ImplementationType = provider.GetType().Name
        };
    }

    private (EStateStoreProviderType providerType, EStateStoreCapabilities capabilities, string displayName) GetProviderMetadata(IStateStore provider)
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
                return (moduleProvider.ProviderType, moduleProvider.Capabilities, moduleProvider.DisplayName);
            }
        }

        if (provider is IMemoryStateStore)
        {
            return (EStateStoreProviderType.Memory,
                EStateStoreCapabilities.KeyScanning | EStateStoreCapabilities.BulkOperations,
                "Memory");
        }

        return (EStateStoreProviderType.Unknown, EStateStoreCapabilities.BulkOperations, "Unknown");
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
            return Res.Fail(BuildDetailedErrorMessage("搜索 Key 失败", ex));
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
        catch (KeyNotFoundException ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load state store key: {Key}", key);
            return Res.Fail(BuildDetailedErrorMessage("获取 Key 失败", ex));
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
            return Res.Fail(BuildDetailedErrorMessage("检查 Key 失败", ex));
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
                    return Res.Fail("ETag 不匹配 - Key 已被其他进程修改");
                }
            }
            else
            {
                await provider.SaveStateAsync(request.Key, value, cancellationToken, request.TTL);
            }

            return Res.Ok("Key 保存成功");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save state store key: {Key}", request.Key);
            return Res.Fail(BuildDetailedErrorMessage("保存 Key 失败", ex));
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
                return Res.Fail("Key 已存在，请使用其他名称或编辑现有 Key");
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
            return Res.Fail(BuildDetailedErrorMessage("创建 Key 失败", ex));
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
                    return Res.Fail("ETag 不匹配 - Key 已被其他进程修改");
                }
            }
            else
            {
                await provider.DeleteStateAsync(key, cancellationToken);
            }

            return Res.Ok("Key 删除成功");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete state store key: {Key}", key);
            return Res.Fail(BuildDetailedErrorMessage("删除 Key 失败", ex));
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
            return Res.Ok($"已删除 {keys.Count} 个 Key");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete state store keys in bulk.");
            return Res.Fail(BuildDetailedErrorMessage("批量删除 Key 失败", ex));
        }
    }

    #endregion

    private static string BuildDetailedErrorMessage(string operation, Exception ex)
    {
        return $"{operation}: {ex.GetMessageRecursively()}";
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
