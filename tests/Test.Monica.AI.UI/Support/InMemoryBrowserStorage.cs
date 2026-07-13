using System.Text.Json;
using Monica.UI.Shell.Support;

namespace Test.Monica.AI.UI.Support;

internal sealed class InMemoryBrowserStorage : IBrowserStorage
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public Func<string, BrowserStorageWriteFailureKind>? WriteFailure { get; set; }

    public InProcessBrowserChatHistoryLock HistoryLock { get; } = new();

    public IReadOnlyDictionary<string, string> Values => _values;

    public List<string> RemovedKeys { get; } = [];

    public List<string> Operations { get; } = [];

    public Task<T> GetAsync<T>(
        string key,
        T defaultValue,
        BrowserStorageType storageType = BrowserStorageType.Local)
    {
        return Task.FromResult(
            _values.TryGetValue(key, out var json)
                ? JsonSerializer.Deserialize<T>(json, JSON_OPTIONS) ?? defaultValue
                : defaultValue);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        BrowserStorageType storageType = BrowserStorageType.Local)
    {
        _ = await TrySetAsync(key, value, storageType);
    }

    public Task<BrowserStorageWriteResult> TrySetAsync<T>(
        string key,
        T value,
        BrowserStorageType storageType = BrowserStorageType.Local)
    {
        var failure = WriteFailure?.Invoke(key) ?? BrowserStorageWriteFailureKind.None;
        if (failure == BrowserStorageWriteFailureKind.None)
        {
            _values[key] = JsonSerializer.Serialize(value, JSON_OPTIONS);
            Operations.Add($"write:{key}");
        }

        return Task.FromResult(new BrowserStorageWriteResult(failure));
    }

    public Task RemoveAsync(
        string key,
        BrowserStorageType storageType = BrowserStorageType.Local)
    {
        _values.Remove(key);
        RemovedKeys.Add(key);
        Operations.Add($"remove:{key}");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetKeysAsync(
        string category,
        BrowserStorageType storageType = BrowserStorageType.Local)
    {
        IReadOnlyList<string> keys = _values.Keys
            .Where(key => key.StartsWith($"{category}:", StringComparison.Ordinal))
            .ToArray();
        return Task.FromResult(keys);
    }

    public Task<int> ClearCategoryAsync(
        string category,
        BrowserStorageType storageType = BrowserStorageType.Local)
    {
        var keys = _values.Keys
            .Where(key => key.StartsWith($"{category}:", StringComparison.Ordinal))
            .ToArray();
        foreach (var key in keys)
        {
            _values.Remove(key);
        }

        return Task.FromResult(keys.Length);
    }

    public void Corrupt(string key)
    {
        _values[key] = JsonSerializer.Serialize("corrupt", JSON_OPTIONS);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
