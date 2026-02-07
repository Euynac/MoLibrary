using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Monica.UI.Services;

/// <summary>
/// Browser storage implementation using JS interop with lazy module loading.
/// All keys are auto-prefixed with "mo:" to avoid collisions.
/// </summary>
public class MoBrowserStorage(IJSRuntime jsRuntime, ILogger<MoBrowserStorage> logger) : IMoBrowserStorage
{
    private const string KeyPrefix = "mo:";
    private const string ModulePath = "./_content/Monica.UI/js/mo-browser-storage.js";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private IJSObjectReference? _module;
    private bool _disposed;

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        return _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
    }

    private static string PrefixKey(string key) => $"{KeyPrefix}{key}";

    public async Task<T> GetAsync<T>(string key, T defaultValue, BrowserStorageType storageType = BrowserStorageType.Local)
    {
        try
        {
            var module = await GetModuleAsync();
            var json = await module.InvokeAsync<string?>("getItem", GetStorageTypeName(storageType), PrefixKey(key));

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
        try
        {
            var module = await GetModuleAsync();
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await module.InvokeVoidAsync("setItem", GetStorageTypeName(storageType), PrefixKey(key), json);
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected, silently ignore
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to set browser storage key '{Key}'", key);
        }
    }

    public async Task RemoveAsync(string key, BrowserStorageType storageType = BrowserStorageType.Local)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("removeItem", GetStorageTypeName(storageType), PrefixKey(key));
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
            var module = await GetModuleAsync();
            var prefix = $"{KeyPrefix}{category}:";
            var keys = await module.InvokeAsync<string[]>("getKeys", GetStorageTypeName(storageType), prefix);
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
            var module = await GetModuleAsync();
            var prefix = $"{KeyPrefix}{category}:";
            return await module.InvokeAsync<int>("clearByPrefix", GetStorageTypeName(storageType), prefix);
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_module != null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit already disconnected
            }
        }

        GC.SuppressFinalize(this);
    }
}