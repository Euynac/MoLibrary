using System.Collections.Concurrent;

namespace Monica.Core.Features.MoScopedData;

/// <summary>
/// Default implementation of <see cref="IMoScopedData" /> for scoped temporary data.
/// </summary>
public class MoScopedDataDefaultScopedProvider : IMoScopedData
{
    /// <summary>
    /// Underlying key/value store for scoped data.
    /// </summary>
    public IDictionary<string, object?> DataDict => _dataDict;
    private ConcurrentDictionary<string, object?> _dataDict { get; } = new();

    /// <summary>
    /// Stores a value under the specified key.
    /// </summary>
    /// <param name="key">The data key.</param>
    /// <param name="value">The value to store.</param>
    public void SetData(string key, object? value = null)
    {
        _dataDict.AddOrUpdate(key, value, (_, _) => value);
    }

    /// <summary>
    /// Gets a value by key.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The data key.</param>
    /// <returns>The stored value, or <see langword="null" /> when it is missing.</returns>
    public T? GetData<T>(string key)
    {
        if (DataDict.TryGetValue(key, out var value) && value is  T directValue)
        {
            return directValue;
        }
        return default;
    }

    /// <summary>
    /// Gets a value by key or returns the supplied fallback.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The data key.</param>
    /// <param name="defaultValue">The fallback value.</param>
    /// <returns>The stored value or <paramref name="defaultValue" />.</returns>
    public T GetData<T>(string key, T defaultValue)
    {
        var result = GetData<T>(key);
        return result ?? defaultValue;
    }

    /// <summary>
    /// Checks whether the specified key exists.
    /// </summary>
    /// <param name="key">The data key.</param>
    /// <returns><see langword="true" /> when the key exists; otherwise, <see langword="false" />.</returns>
    public bool HasData(string key)
    {
        return _dataDict.ContainsKey(key);
    }

    /// <summary>
    /// Removes the specified key.
    /// </summary>
    /// <param name="key">The data key.</param>
    /// <returns><see langword="true" /> when the key was removed; otherwise, <see langword="false" />.</returns>
    public bool RemoveData(string key)
    {
        return _dataDict.TryRemove(key, out _);
    }

    /// <summary>
    /// Clears all stored data.
    /// </summary>
    public void Clear()
    {
        _dataDict.Clear();
    }
} 
