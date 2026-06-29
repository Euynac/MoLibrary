namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Contains a startup snapshot of options loaded from Monica effective values.
/// </summary>
public sealed class MonicaEffectiveOptionsSnapshot
{
    private readonly IReadOnlyDictionary<Type, object> _optionsByType;

    internal MonicaEffectiveOptionsSnapshot(IReadOnlyDictionary<Type, object> optionsByType)
    {
        _optionsByType = optionsByType;
    }

    /// <summary>
    /// Gets one options object from the snapshot.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <returns>The loaded options object.</returns>
    public TOptions Get<TOptions>()
        where TOptions : class, new()
    {
        return (TOptions)Get(typeof(TOptions));
    }

    /// <summary>
    /// Gets one options object from the snapshot.
    /// </summary>
    /// <param name="optionsType">The options type.</param>
    /// <returns>The loaded options object.</returns>
    public object Get(Type optionsType)
    {
        ArgumentNullException.ThrowIfNull(optionsType);

        if (_optionsByType.TryGetValue(optionsType, out var options))
        {
            return options;
        }

        throw new KeyNotFoundException(
            $"Options type '{optionsType.FullName ?? optionsType.Name}' was not loaded into this Monica effective options snapshot.");
    }
}
