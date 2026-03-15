namespace Monica.Core.Features.MoScopedData;

/// <summary>
/// Stores temporary key/value data within the current scoped lifetime.
/// </summary>
public interface IMoScopedData
{
    /// <summary>
    /// Underlying key/value store for scoped data.
    /// </summary>
    public IDictionary<string, object?> DataDict { get; }
    
    /// <summary>
    /// Stores a value under the specified key.
    /// </summary>
    /// <param name="key">The data key.</param>
    /// <param name="value">The value to store.</param>
    void SetData(string key, object? value = null);
    
    /// <summary>
    /// Gets a value by key.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The data key.</param>
    /// <returns>The stored value, or the default value of <typeparamref name="T" /> when not found.</returns>
    T? GetData<T>(string key);
    
    /// <summary>
    /// Gets a value by key or returns the supplied fallback.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The data key.</param>
    /// <param name="defaultValue">The fallback value.</param>
    /// <returns>The stored value or <paramref name="defaultValue" />.</returns>
    T GetData<T>(string key, T defaultValue);
    
    /// <summary>
    /// Checks whether the specified key exists.
    /// </summary>
    /// <param name="key">The data key.</param>
    /// <returns><see langword="true" /> when the key exists; otherwise, <see langword="false" />.</returns>
    bool HasData(string key);
    
    /// <summary>
    /// Removes the specified key.
    /// </summary>
    /// <param name="key">The data key.</param>
    /// <returns><see langword="true" /> when the key was removed; otherwise, <see langword="false" />.</returns>
    bool RemoveData(string key);
    
    /// <summary>
    /// Clears all stored data.
    /// </summary>
    void Clear();
}
