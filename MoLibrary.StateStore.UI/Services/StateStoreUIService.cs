using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.StateStore.Modules;
using MoLibrary.StateStore.Providers;
using MoLibrary.StateStore.UI.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.StateStore.UI.Services;

/// <summary>
/// StateStore UI 服务 - 提供 Provider 发现和 Key 管理操作
/// </summary>
public class StateStoreUIService(
    IServiceProvider serviceProvider,
    IOptions<ModuleStateStoreOption> stateStoreOption,
    ILogger<StateStoreUIService> logger)
{
    /// <summary>
    /// Cached provider snapshots for efficient lookup
    /// </summary>
    private List<ModuleSnapshot>? _providerSnapshots;

    /// <summary>
    /// Gets or initializes the cached provider snapshots
    /// </summary>
    private List<ModuleSnapshot> ProviderSnapshots =>
        _providerSnapshots ??= MoModuleRegisterCentre.GetModuleProviders(EMoModuleKey.StateStore);

    #region Provider Discovery

    /// <summary>
    /// 获取所有已注册的 StateStore Provider
    /// </summary>
    public Res<List<StateStoreProviderInfo>> GetRegisteredProviders()
    {
        try
        {
            var providers = new List<StateStoreProviderInfo>();

            // 1. 从模块注册中获取 Keyed 服务键
            var keyedServiceKeys = MoModuleRegisterCentre.GetKeyedServiceKeys(typeof(ModuleStateStore));

            // 2. 获取非 Keyed 的默认 Provider
            var defaultProvider = serviceProvider.GetService<IMoStateStore>();
            if (defaultProvider != null)
            {
                providers.Add(CreateProviderInfo(null, defaultProvider));
            }

            // 3. 获取 Keyed Provider
            foreach (var key in keyedServiceKeys)
            {
                try
                {
                    var keyedProvider = serviceProvider.GetKeyedService<IMoStateStore>(key);
                    if (keyedProvider != null)
                    {
                        providers.Add(CreateProviderInfo(key, keyedProvider));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "解析 Keyed StateStore Provider 失败: {Key}", key);
                }
            }

            return Res.Ok(providers);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取已注册 Provider 失败");
            return Res.Fail($"获取已注册 Provider 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据服务键获取 Provider
    /// </summary>
    public Res<IMoStateStore> GetProvider(string? serviceKey)
    {
        try
        {
            IMoStateStore? provider = serviceKey == null
                ? serviceProvider.GetService<IMoStateStore>()
                : serviceProvider.GetKeyedService<IMoStateStore>(serviceKey);

            if (provider == null)
            {
                return Res.Fail($"未找到 Provider: {serviceKey ?? "默认"}");
            }

            return Res.Ok(provider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取 Provider 失败: {Key}", serviceKey);
            return Res.Fail($"获取 Provider 失败: {ex.Message}");
        }
    }

    private StateStoreProviderInfo CreateProviderInfo(string? serviceKey, IMoStateStore provider)
    {
        var (providerType, capabilities, displayName) = GetProviderMetadata(provider);
        var (optionType, optionInstance) = GetProviderOptionInfo(serviceKey, provider);

        // Determine if this is the default IMoStateStore (only for non-keyed providers)
        var isDefaultIMoStateStore = false;
        if (serviceKey == null)
        {
            var useDistributed = stateStoreOption.Value.UseDistributedProviderAsDefault;
            var isDistributed = provider is IDistributedStateStore;
            isDefaultIMoStateStore = useDistributed == isDistributed;
        }

        return new StateStoreProviderInfo
        {
            ServiceKey = serviceKey,
            ProviderType = providerType,
            Capabilities = capabilities,
            IsDistributed = provider is IDistributedStateStore,
            IsDefaultIMoStateStore = isDefaultIMoStateStore,
            OptionType = optionType,
            OptionInstance = optionInstance,
            ImplementationType = provider.GetType().Name
        };
    }

    /// <summary>
    /// Gets provider metadata from the registered IStateStoreModuleProvider
    /// </summary>
    private (EStateStoreProviderType providerType, EStateStoreCapabilities capabilities, string displayName) GetProviderMetadata(IMoStateStore provider)
    {
        // Try to find the matching provider module based on the provider's type name
        var providerTypeName = provider.GetType().FullName ?? "";

        foreach (var snapshot in ProviderSnapshots)
        {
            if (snapshot.ModuleInstance is not IStateStoreModuleProvider moduleProvider) continue;

            // Match by checking if the provider type name contains the module's display name
            if (providerTypeName.Contains(moduleProvider.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return (moduleProvider.ProviderType, moduleProvider.Capabilities, moduleProvider.DisplayName);
            }
        }

        // Fallback for memory provider or unknown types
        if (provider is IMemoryStateStore)
        {
            return (EStateStoreProviderType.Memory,
                    EStateStoreCapabilities.KeyScanning | EStateStoreCapabilities.BulkOperations,
                    "Memory");
        }

        return (EStateStoreProviderType.Unknown, EStateStoreCapabilities.BulkOperations, "Unknown");
    }

    /// <summary>
    /// Gets provider option information using ModuleSnapshot's generic option retrieval
    /// </summary>
    private (Type? optionType, object? optionInstance) GetProviderOptionInfo(string? serviceKey, IMoStateStore provider)
    {
        try
        {
            var providerTypeName = provider.GetType().FullName ?? "";

            // Find the matching provider module snapshot
            foreach (var snapshot in ProviderSnapshots)
            {
                if (snapshot.ModuleInstance is not IStateStoreModuleProvider moduleProvider) continue;

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
            logger.LogWarning(ex, "获取 Provider 配置失败: {ServiceKey}", serviceKey);
            return (null, null);
        }
    }

    #endregion

    #region Key Operations

    /// <summary>
    /// 扫描匹配模式的 Key
    /// </summary>
    public async Task<Res<KeyScanResult>> ScanKeysAsync(
        string? serviceKey,
        string pattern = "*",
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (GetProvider(serviceKey).IsFailed(out var error, out var provider))
            return Res.Fail(error);

        try
        {
            var keys = await provider.ScanKeysAsync(pattern, cancellationToken);

            return Res.Ok(new KeyScanResult
            {
                Keys = keys.Take(limit).ToList(),
                TotalCount = keys.Count,
                HasMore = keys.Count > limit
            });
        }
        catch (NotImplementedException)
        {
            return Res.Fail("此 Provider 不支持 Key 扫描，请输入完整 Key 名称进行查询");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "扫描 Key 失败: {Pattern}", pattern);
            return Res.Fail($"扫描 Key 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取单个 Key 的值和元数据
    /// </summary>
    public async Task<Res<StateStoreKeyInfo>> GetKeyAsync(
        string? serviceKey,
        string key,
        CancellationToken cancellationToken = default)
    {
        if (GetProvider(serviceKey).IsFailed(out var error, out var provider))
            return Res.Fail(error);

        try
        {
            string? rawValue = null;
            string? etag = null;

            var (value, etagValue) = await provider.GetStateAndETagAsync<object>(key, cancellationToken);
            rawValue = value != null ? JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) : null;
            etag = etagValue;

            return Res.Ok(new StateStoreKeyInfo
            {
                Key = key,
                RawValue = rawValue,
                IsValueLoaded = true,
                ETag = etag
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取 Key 失败: {Key}", key);
            return Res.Ok(new StateStoreKeyInfo
            {
                Key = key,
                IsValueLoaded = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 检查 Key 是否存在
    /// </summary>
    public async Task<Res<bool>> KeyExistsAsync(
        string? serviceKey,
        string key,
        CancellationToken cancellationToken = default)
    {
        if (GetProvider(serviceKey).IsFailed(out var error, out var provider))
            return Res.Fail(error);

        try
        {
            var exists = await provider.ExistAsync(key, cancellationToken);
            return Res.Ok(exists);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "检查 Key 是否存在失败: {Key}", key);
            return Res.Fail($"检查 Key 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建或更新 Key
    /// </summary>
    public async Task<Res> SaveKeyAsync(
        string? serviceKey,
        StateStoreKeyUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (GetProvider(serviceKey).IsFailed(out var error, out var provider))
            return error;

        try
        {
            // 解析 JSON 值
            object? value;
            try
            {
                value = JsonSerializer.Deserialize<object>(request.Value);
            }
            catch
            {
                // 如果不是有效 JSON，作为字符串处理
                value = request.Value;
            }

            // 如果提供了 ETag，使用乐观并发控制
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
            logger.LogError(ex, "保存 Key 失败: {Key}", request.Key);
            return Res.Fail($"保存 Key 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除 Key
    /// </summary>
    public async Task<Res> DeleteKeyAsync(
        string? serviceKey,
        string key,
        string? etag = null,
        CancellationToken cancellationToken = default)
    {
        if (GetProvider(serviceKey).IsFailed(out var error, out var provider))
            return error;

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
            logger.LogError(ex, "删除 Key 失败: {Key}", key);
            return Res.Fail($"删除 Key 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 批量删除 Key
    /// </summary>
    public async Task<Res> DeleteKeysAsync(
        string? serviceKey,
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default)
    {
        if (GetProvider(serviceKey).IsFailed(out var error, out var provider))
            return error;

        try
        {
            await provider.DeleteBulkStateAsync(keys, cancellationToken);
            return Res.Ok($"已删除 {keys.Count} 个 Key");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "批量删除 Key 失败");
            return Res.Fail($"批量删除 Key 失败: {ex.Message}");
        }
    }

    #endregion
}
