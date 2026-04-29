using System.Collections.Immutable;

namespace Monica.AI.Services.Support;

/// <summary>
/// Immutable per-run context used by tools and skills to read module-owned session state.
/// </summary>
public sealed class AIChatRuntimeContext
{
    /// <summary>
    /// Empty runtime context.
    /// </summary>
    public static AIChatRuntimeContext Empty { get; } = new(ImmutableDictionary<string, object?>.Empty);

    private readonly ImmutableDictionary<string, object?> _values;

    private AIChatRuntimeContext(ImmutableDictionary<string, object?> values)
    {
        _values = values;
    }

    /// <summary>
    /// Returns a new context snapshot with the supplied key assigned to the supplied value.
    /// </summary>
    /// <typeparam name="T">Value type associated with the key.</typeparam>
    /// <param name="key">The typed runtime context key.</param>
    /// <param name="value">The value to store.</param>
    /// <returns>A new immutable context snapshot.</returns>
    public AIChatRuntimeContext Set<T>(AIChatRuntimeContextKey<T> key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new AIChatRuntimeContext(_values.SetItem(key.Name, value));
    }

    /// <summary>
    /// Attempts to read a typed value from the context.
    /// </summary>
    /// <typeparam name="T">Value type associated with the key.</typeparam>
    /// <param name="key">The typed runtime context key.</param>
    /// <param name="value">The resolved value when present and type-compatible.</param>
    /// <returns><see langword="true"/> when the key exists and contains a value of type <typeparamref name="T"/>.</returns>
    public bool TryGet<T>(AIChatRuntimeContextKey<T> key, out T value)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_values.TryGetValue(key.Name, out var rawValue) && rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Reads a typed value from the context or returns the default value for the type.
    /// </summary>
    /// <typeparam name="T">Value type associated with the key.</typeparam>
    /// <param name="key">The typed runtime context key.</param>
    /// <returns>The stored value, or <see langword="default"/> when absent.</returns>
    public T? GetOrDefault<T>(AIChatRuntimeContextKey<T> key)
    {
        return TryGet(key, out T value) ? value : default;
    }
}
