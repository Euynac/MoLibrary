using System.Collections;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Module;
using Monica.Core.Module.Models;
using Monica.Modules;
using Monica.StateStore.Providers;
using Monica.StateStore.UI.Models;
using Monica.StateStore.UI.Services.Browser;
using Monica.Tool.MoResponse;

namespace Monica.StateStore.UI.Services;

/// <summary>
/// UI service for state store provider discovery and dashboard operations.
/// </summary>
public class StateStoreUIService(
    IServiceProvider serviceProvider,
    IOptions<ModuleStateStoreOption> stateStoreOption,
    IEnumerable<IStateStoreBrowserApi> browserApis,
    ILogger<StateStoreUIService> logger)
{
    private static readonly HashSet<string> IgnoredOptionProperties =
    [
        "Logger",
        "DisableModuleIfHasException",
        "IsDisabled"
    ];

    private static readonly string[] SensitiveOptionFragments =
    [
        "password",
        "secret",
        "token",
        "connectionstring",
        "apiKey",
        "clientSecret"
    ];

    private readonly IReadOnlyList<IStateStoreBrowserApi> _browserApis = browserApis.ToList();
    private List<ModuleSnapshot>? _providerSnapshots;

    private List<ModuleSnapshot> ProviderSnapshots =>
        _providerSnapshots ??= MoModuleRegisterCentre.GetModuleProviders(EMoModuleKey.StateStore);

    #region Provider Discovery

    public Res<List<StateStoreProviderInfo>> GetRegisteredProviders()
    {
        try
        {
            var providers = new List<StateStoreProviderInfo>();
            var keyedServiceKeys = MoModuleRegisterCentre.GetKeyedServiceKeys(typeof(ModuleStateStore));

            var defaultProvider = serviceProvider.GetService<IMoStateStore>();
            if (defaultProvider != null)
            {
                providers.Add(CreateProviderInfo(null, defaultProvider));
            }

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
                    logger.LogWarning(ex, "Failed to resolve keyed state store provider: {Key}", key);
                }
            }

            return Res.Ok(providers
                .OrderByDescending(provider => provider.IsDefaultIMoStateStore)
                .ThenBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get registered state store providers.");
            return Res.Fail($"获取已注册 Provider 失败: {ex.Message}");
        }
    }

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
            logger.LogError(ex, "Failed to get state store provider: {ServiceKey}", serviceKey);
            return Res.Fail($"获取 Provider 失败: {ex.Message}");
        }
    }

    private StateStoreProviderInfo CreateProviderInfo(string? serviceKey, IMoStateStore provider)
    {
        var (providerType, capabilities, providerDisplayName) = GetProviderMetadata(provider);
        var browserApi = GetBrowserApi(providerType, provider);
        var browserFeatures = browserApi.GetFeatures(provider);
        var defaultSearchMode = browserApi.GetDefaultSearchMode(provider);
        var (optionType, optionInstance) = GetProviderOptionInfo(serviceKey, provider);
        var configurationEntries = CreateConfigurationEntries(optionType, optionInstance);

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
            ProviderDisplayName = providerDisplayName,
            ProviderType = providerType,
            Capabilities = capabilities,
            BrowserFeatures = browserFeatures,
            DefaultSearchMode = defaultSearchMode,
            IsDistributed = provider is IDistributedStateStore,
            IsDefaultIMoStateStore = isDefaultIMoStateStore,
            OptionType = optionType,
            OptionInstance = optionInstance,
            ConfigurationEntries = configurationEntries,
            ImplementationType = provider.GetType().Name
        };
    }

    private (EStateStoreProviderType providerType, EStateStoreCapabilities capabilities, string displayName) GetProviderMetadata(IMoStateStore provider)
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

    private (Type? optionType, object? optionInstance) GetProviderOptionInfo(string? serviceKey, IMoStateStore provider)
    {
        try
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
                    return snapshot.GetKeyedOption(serviceProvider, serviceKey);
                }
            }

            return (null, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get provider option snapshot: {ServiceKey}", serviceKey);
            return (null, null);
        }
    }

    private IStateStoreBrowserApi GetBrowserApi(EStateStoreProviderType providerType, IMoStateStore provider)
    {
        return _browserApis.First(api => api.CanHandle(providerType, provider));
    }

    private static IReadOnlyList<StateStoreProviderConfigEntry> CreateConfigurationEntries(Type? optionType, object? optionInstance)
    {
        if (optionType is null || optionInstance is null)
        {
            return [];
        }

        var entries = new List<StateStoreProviderConfigEntry>();
        AppendConfigurationEntries(entries, optionInstance, prefix: string.Empty, depth: 0);

        return entries
            .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendConfigurationEntries(
        List<StateStoreProviderConfigEntry> entries,
        object source,
        string prefix,
        int depth)
    {
        if (depth > 2)
        {
            return;
        }

        foreach (var property in source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0 || IgnoredOptionProperties.Contains(property.Name))
            {
                continue;
            }

            object? value;
            try
            {
                value = property.GetValue(source);
            }
            catch
            {
                continue;
            }

            if (value is null)
            {
                continue;
            }

            var path = string.IsNullOrWhiteSpace(prefix) ? property.Name : $"{prefix}.{property.Name}";

            if (TryFormatScalarValue(property.Name, value, out var scalarValue))
            {
                entries.Add(new StateStoreProviderConfigEntry
                {
                    Path = path,
                    Label = HumanizePropertyName(property.Name),
                    Value = scalarValue
                });
                continue;
            }

            if (TryFormatSequenceValue(property.Name, value, out var sequenceValue))
            {
                entries.Add(new StateStoreProviderConfigEntry
                {
                    Path = path,
                    Label = HumanizePropertyName(property.Name),
                    Value = sequenceValue
                });
                continue;
            }

            AppendConfigurationEntries(entries, value, path, depth + 1);
        }
    }

    private static bool TryFormatScalarValue(string propertyName, object value, out string formattedValue)
    {
        switch (value)
        {
            case string text when string.IsNullOrWhiteSpace(text):
                formattedValue = string.Empty;
                return false;
            case string text:
                formattedValue = IsSensitiveOption(propertyName) ? "••••••" : text;
                return true;
            case bool flag:
                formattedValue = flag ? "Enabled" : "Disabled";
                return true;
            case TimeSpan timeSpan:
                formattedValue = timeSpan.ToString("c", CultureInfo.InvariantCulture);
                return true;
            case Enum enumValue:
                formattedValue = enumValue.ToString();
                return true;
            case Uri uri:
                formattedValue = uri.ToString();
                return true;
            case DateTime dateTime:
                formattedValue = dateTime.ToString("u", CultureInfo.InvariantCulture);
                return true;
            case DateTimeOffset dateTimeOffset:
                formattedValue = dateTimeOffset.ToString("u", CultureInfo.InvariantCulture);
                return true;
        }

        var valueType = value.GetType();
        if (valueType.IsPrimitive || value is decimal)
        {
            formattedValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(formattedValue);
        }

        formattedValue = string.Empty;
        return false;
    }

    private static bool TryFormatSequenceValue(string propertyName, object value, out string formattedValue)
    {
        if (value is string || value is not IEnumerable enumerable)
        {
            formattedValue = string.Empty;
            return false;
        }

        var items = enumerable
            .Cast<object?>()
            .Where(item => item is not null)
            .Select(item => TryFormatScalarValue(propertyName, item!, out var itemValue)
                ? itemValue
                : item!.ToString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(6)
            .ToList();

        if (items.Count == 0)
        {
            formattedValue = string.Empty;
            return false;
        }

        formattedValue = string.Join(", ", items);
        return true;
    }

    private static bool IsSensitiveOption(string propertyName)
    {
        return SensitiveOptionFragments.Any(fragment =>
            propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static string HumanizePropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return propertyName;
        }

        return string.Concat(propertyName.Select((character, index) =>
            index > 0 && char.IsUpper(character) && !char.IsUpper(propertyName[index - 1])
                ? $" {character}"
                : character.ToString()));
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
            return Res.Fail($"搜索 Key 失败: {ex.Message}");
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
            return Res.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load state store key: {Key}", key);
            return Res.Fail($"获取 Key 失败: {ex.Message}");
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
            return Res.Fail($"检查 Key 失败: {ex.Message}");
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
            return Res.Fail($"保存 Key 失败: {ex.Message}");
        }
    }

    public async Task<Res<StateStoreKeyInfo>> CreateKeyAsync(
        string? serviceKey,
        StateStoreKeyUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (GetProvider(serviceKey).IsFailed(out var error, out var provider))
        {
            return Res.Fail(error);
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
            return Res.Fail($"创建 Key 失败: {ex.Message}");
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
            return Res.Fail($"删除 Key 失败: {ex.Message}");
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
            return Res.Fail($"批量删除 Key 失败: {ex.Message}");
        }
    }

    #endregion

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
