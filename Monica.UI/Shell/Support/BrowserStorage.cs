using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Monica.UI.Shell.Support;

/// <summary>
/// Browser storage implementation using JS interop with lazy module loading.
/// All keys are auto-prefixed with "mo:" to avoid collisions.
/// </summary>
public class BrowserStorage(IJSRuntime jsRuntime, ILogger<BrowserStorage> logger) : IBrowserStorage
{
    private const string KEY_PREFIX = "mo:";
    private const string MODULE_PATH = "./_content/Monica.UI/js/mo-browser-storage.js";
    private const string QUOTA_EXCEEDED_ERROR = "quota-exceeded";
    private const string STORAGE_UNAVAILABLE_ERROR = "storage-unavailable";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly JsModuleSession _module = new(jsRuntime, MODULE_PATH);

    private static string PrefixKey(string key) => $"{KEY_PREFIX}{key}";

    public async Task<T> GetAsync<T>(string key, T defaultValue, BrowserStorageType storageType = BrowserStorageType.Local)
    {
        try
        {
            var json = await _module.InvokeAsync<string?>(
                "getItem",
                GetStorageTypeName(storageType),
                PrefixKey(key));

            if (string.IsNullOrEmpty(json))
                return defaultValue;

            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? defaultValue;
        }
        catch (JSDisconnectedException)
        {
            return defaultValue;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to get browser storage key '{Key}'", key);
            return defaultValue;
        }
    }

    public async Task SetAsync<T>(string key, T value, BrowserStorageType storageType = BrowserStorageType.Local)
    {
        _ = await TrySetAsync(key, value, storageType);
    }

    public async Task<BrowserStorageWriteResult> TrySetAsync<T>(
        string key,
        T value,
        BrowserStorageType storageType = BrowserStorageType.Local)
    {
        string json;

        try
        {
            json = JsonSerializer.Serialize(value, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to serialize browser storage key '{Key}'", key);
            return new BrowserStorageWriteResult(BrowserStorageWriteFailureKind.SerializationFailed);
        }

        try
        {
            var failureCode = await _module.InvokeAsync<string?>(
                "trySetItem",
                GetStorageTypeName(storageType),
                PrefixKey(key),
                json);

            var result = new BrowserStorageWriteResult(MapWriteFailure(failureCode));
            if (!result.Succeeded)
            {
                logger.LogDebug(
                    "Failed to set browser storage key '{Key}': {FailureKind}",
                    key,
                    result.FailureKind);
            }

            return result;
        }
        catch (JSDisconnectedException)
        {
            return new BrowserStorageWriteResult(BrowserStorageWriteFailureKind.StorageUnavailable);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to set browser storage key '{Key}'", key);
            return new BrowserStorageWriteResult(BrowserStorageWriteFailureKind.Unknown);
        }
    }

    public async Task RemoveAsync(string key, BrowserStorageType storageType = BrowserStorageType.Local)
    {
        try
        {
            await _module.InvokeVoidAsync(
                "removeItem",
                GetStorageTypeName(storageType),
                PrefixKey(key));
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected, silently ignore
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to remove browser storage key '{Key}'", key);
        }
    }

    public async Task<IReadOnlyList<string>> GetKeysAsync(string category, BrowserStorageType storageType = BrowserStorageType.Local)
    {
        try
        {
            var prefix = $"{KEY_PREFIX}{category}:";
            var keys = await _module.InvokeAsync<string[]>(
                "getKeys",
                GetStorageTypeName(storageType),
                prefix);
            return keys;
        }
        catch (JSDisconnectedException)
        {
            return [];
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to get browser storage keys for category '{Category}'", category);
            return [];
        }
    }

    public async Task<int> ClearCategoryAsync(string category, BrowserStorageType storageType = BrowserStorageType.Local)
    {
        try
        {
            var prefix = $"{KEY_PREFIX}{category}:";
            return await _module.InvokeAsync<int>(
                "clearByPrefix",
                GetStorageTypeName(storageType),
                prefix);
        }
        catch (JSDisconnectedException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to clear browser storage category '{Category}'", category);
            return 0;
        }
    }

    private static string GetStorageTypeName(BrowserStorageType storageType) =>
        storageType == BrowserStorageType.Session ? "session" : "local";

    private static BrowserStorageWriteFailureKind MapWriteFailure(string? failureCode) => failureCode switch
    {
        null => BrowserStorageWriteFailureKind.None,
        QUOTA_EXCEEDED_ERROR => BrowserStorageWriteFailureKind.QuotaExceeded,
        STORAGE_UNAVAILABLE_ERROR => BrowserStorageWriteFailureKind.StorageUnavailable,
        _ => BrowserStorageWriteFailureKind.Unknown
    };

    public async ValueTask DisposeAsync()
    {
        await _module.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
