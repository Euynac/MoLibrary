using Monica.Core.Features.MoScopedData;

namespace Monica.Repository.Transaction;

/// <summary>
/// Default implementation for scoped ambient data, used to temporarily store and manage state within a scoped lifetime.
/// </summary>
public class MoScopedDataUnitOfWorkProvider(IMoUnitOfWorkManager manager) : IMoScopedData
{
    /// <summary>
    /// Data dictionary used to store key-value pairs.
    /// </summary>
    public IDictionary<string, object?> DataDict => manager.Current?.Items ?? new Dictionary<string, object?>();

    /// <summary>
    /// Sets a data value.
    /// </summary>
    /// <param name="key">Data key.</param>
    /// <param name="value">Data value.</param>
    public void SetData(string key, object? value = null)
    {
        var current = manager.Current;
        if (current != null)
        {
            current.Items[key] = value;
        }
    }

    /// <summary>
    /// Gets a data value.
    /// </summary>
    /// <typeparam name="T">Data type.</typeparam>
    /// <param name="key">Data key.</param>
    /// <returns>The data value, or the default value if it does not exist.</returns>
    public T? GetData<T>(string key)
    {
        var current = manager.Current;
        if (current?.Items.TryGetValue(key, out var value) == true && value is T typedValue)
        {
            return typedValue;
        }
        return default(T);
    }

    /// <summary>
    /// Gets a data value, or returns the specified default value when the key does not exist.
    /// </summary>
    /// <typeparam name="T">Data type.</typeparam>
    /// <param name="key">Data key.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <returns>The data value or the provided default value.</returns>
    public T GetData<T>(string key, T defaultValue)
    {
        var current = manager.Current;
        if (current?.Items.TryGetValue(key, out var value) == true && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Checks whether a given key exists.
    /// </summary>
    /// <param name="key">Data key.</param>
    /// <returns><c>true</c> if the key exists; otherwise, <c>false</c>.</returns>
    public bool HasData(string key)
    {
        var current = manager.Current;
        return current?.Items.ContainsKey(key) == true;
    }

    /// <summary>
    /// Removes the specified data entry.
    /// </summary>
    /// <param name="key">Data key.</param>
    /// <returns><c>true</c> if removal succeeds; otherwise, <c>false</c>.</returns>
    public bool RemoveData(string key)
    {
        var current = manager.Current;
        if (current != null)
        {
            return current.Items.Remove(key);
        }
        return false;
    }

    /// <summary>
    /// Clears all data.
    /// </summary>
    public void Clear()
    {
        var current = manager.Current;
        current?.Items.Clear();
    }
}
