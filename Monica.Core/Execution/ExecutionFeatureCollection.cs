using System.Diagnostics.CodeAnalysis;

namespace Monica.Core.Execution;

/// <summary>
/// Stores invocation-specific, strongly typed metadata supplied by execution adapters.
/// </summary>
/// <remarks>
/// Features are keyed by their exact generic type. A feature registered as a concrete type is not implicitly returned
/// for one of its interfaces. The collection belongs to one invocation and is not thread-safe.
/// </remarks>
public sealed class ExecutionFeatureCollection
{
    private readonly Dictionary<Type, object> _features = [];

    /// <summary>
    /// Gets the number of registered features.
    /// </summary>
    public int Count => _features.Count;

    /// <summary>
    /// Adds or replaces a feature under its exact generic type.
    /// </summary>
    /// <typeparam name="TFeature">The feature contract used as the lookup key.</typeparam>
    /// <param name="feature">The feature value.</param>
    public void Set<TFeature>(TFeature feature)
        where TFeature : notnull
    {
        ArgumentNullException.ThrowIfNull(feature);
        _features[typeof(TFeature)] = feature;
    }

    /// <summary>
    /// Attempts to retrieve a feature registered under its exact generic type.
    /// </summary>
    /// <typeparam name="TFeature">The feature contract used as the lookup key.</typeparam>
    /// <param name="feature">The registered feature when found.</param>
    /// <returns><see langword="true"/> when a feature is registered; otherwise <see langword="false"/>.</returns>
    public bool TryGet<TFeature>([MaybeNullWhen(false)] out TFeature feature)
        where TFeature : notnull
    {
        if (_features.TryGetValue(typeof(TFeature), out var value))
        {
            feature = (TFeature)value;
            return true;
        }

        feature = default;
        return false;
    }

    /// <summary>
    /// Gets a required feature registered under its exact generic type.
    /// </summary>
    /// <typeparam name="TFeature">The feature contract used as the lookup key.</typeparam>
    /// <returns>The registered feature.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the requested feature has not been registered.</exception>
    public TFeature GetRequired<TFeature>()
        where TFeature : notnull
    {
        if (TryGet<TFeature>(out var feature))
        {
            return feature;
        }

        throw new InvalidOperationException(
            $"Execution feature '{typeof(TFeature).FullName}' has not been registered for this invocation.");
    }

    /// <summary>
    /// Removes a feature registered under its exact generic type.
    /// </summary>
    /// <typeparam name="TFeature">The feature contract used as the lookup key.</typeparam>
    /// <returns><see langword="true"/> when the feature was removed; otherwise <see langword="false"/>.</returns>
    public bool Remove<TFeature>()
        where TFeature : notnull
    {
        return _features.Remove(typeof(TFeature));
    }
}
