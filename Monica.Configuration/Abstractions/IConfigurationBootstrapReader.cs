using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Reads Monica configuration values before the dependency injection container is available.
/// </summary>
public interface IConfigurationBootstrapReader
{
    /// <summary>
    /// Reads flat projected values for the supplied definitions.
    /// </summary>
    /// <param name="definitions">The definitions to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Flat Microsoft configuration key/value pairs.</returns>
    Task<IReadOnlyDictionary<string, string?>> ReadFlatValuesAsync(
        IReadOnlyList<ConfigurationDefinition> definitions,
        CancellationToken cancellationToken);

    /// <summary>
    /// Binds a bootstrap options instance.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <param name="definition">The target definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bound options instance, or null when unavailable.</returns>
    Task<TOptions?> GetOptionsAsync<TOptions>(
        ConfigurationDefinition definition,
        CancellationToken cancellationToken)
        where TOptions : class;
}
