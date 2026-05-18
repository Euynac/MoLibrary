namespace Monica.UnitTests.Hosting;

/// <summary>
/// Configures per-test service replacements for a sociable Monica test scope.
/// </summary>
public interface ISeamReplacementBuilder
{
    /// <summary>
    /// Replaces <typeparamref name="TService"/> with the supplied singleton instance.
    /// </summary>
    ISeamReplacementBuilder With<TService>(TService instance)
        where TService : class;

    /// <summary>
    /// Replaces <typeparamref name="TService"/> with a scoped factory.
    /// </summary>
    ISeamReplacementBuilder With<TService>(Func<IServiceProvider, TService> factory)
        where TService : class;

    /// <summary>
    /// Registers a decorator implementation for <typeparamref name="TService"/>.
    /// </summary>
    ISeamReplacementBuilder Decorate<TService, TDecorator>()
        where TService : class
        where TDecorator : class, TService;

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
