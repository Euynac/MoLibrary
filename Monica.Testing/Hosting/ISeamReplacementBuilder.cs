namespace Monica.Testing.Hosting;

/// <summary>
/// Configures boundary replacements before a Monica test scenario host is built.
/// </summary>
public interface ISeamReplacementBuilder
{
    /// <summary>
    /// Replaces <typeparamref name="TService"/> with the supplied scenario-owned singleton instance.
    /// </summary>
    ISeamReplacementBuilder With<TService>(TService instance)
        where TService : class;

    /// <summary>
    /// Replaces <typeparamref name="TService"/> with a scoped factory.
    /// </summary>
    ISeamReplacementBuilder With<TService>(Func<IServiceProvider, TService> factory)
        where TService : class;

    /// <summary>
    /// Creates and registers an NSubstitute substitute.
    /// </summary>
    ISeamReplacementBuilder Substitute<TService>(out TService substitute)
        where TService : class;

    /// <summary>
    /// Registers a named HTTP client for tests that intentionally exercise HTTP adapters.
    /// </summary>
    ISeamReplacementBuilder WithHttpClient(string name, HttpClient client);
}
