using Monica.Configuration.Models;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Reads Monica-managed options from an effective-value store before the application service provider is available.
/// </summary>
/// <remarks>
/// This reader is intended for startup code that must configure modules before dependency injection has been built.
/// It returns a startup snapshot and does not replace <c>IOptionsMonitor&lt;TOptions&gt;</c> or runtime reload flows.
/// </remarks>
public interface IMonicaEffectiveOptionsReader : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Reads one options object from the Monica effective-value store.
    /// </summary>
    /// <typeparam name="TOptions">The options type marked with <see cref="Configuration.Annotations.ConfigurationAttribute"/>.</typeparam>
    /// <returns>The effective options object.</returns>
    TOptions Get<TOptions>()
        where TOptions : class, new();

    /// <summary>
    /// Reads one options object from the Monica effective-value store.
    /// </summary>
    /// <typeparam name="TOptions">The options type marked with <see cref="Configuration.Annotations.ConfigurationAttribute"/>.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The effective options object.</returns>
    Task<TOptions> GetAsync<TOptions>(CancellationToken cancellationToken = default)
        where TOptions : class, new();

    /// <summary>
    /// Reads multiple options objects from the Monica effective-value store in one batch.
    /// </summary>
    /// <param name="optionsTypes">The options types marked with <see cref="Configuration.Annotations.ConfigurationAttribute"/>.</param>
    /// <returns>A snapshot containing the effective options objects.</returns>
    MonicaEffectiveOptionsSnapshot GetMany(params Type[] optionsTypes);

    /// <summary>
    /// Reads multiple options objects from the Monica effective-value store in one batch.
    /// </summary>
    /// <param name="optionsTypes">The options types marked with <see cref="Configuration.Annotations.ConfigurationAttribute"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A snapshot containing the effective options objects.</returns>
    Task<MonicaEffectiveOptionsSnapshot> GetManyAsync(
        IReadOnlyCollection<Type> optionsTypes,
        CancellationToken cancellationToken = default);
}
