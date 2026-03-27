using Monica.AI.Models;

namespace Monica.AI.Abstractions;

/// <summary>
/// AI Provider factory interface, used to create and manage Provider instances
/// </summary>
public interface IAIProviderFactory
{
    /// <summary>
    /// Get the Provider with the specified ID
    /// </summary>
    /// <param name="providerId">Provider ID</param>
    /// <returns>Provider instance, or null if it does not exist</returns>
    IAIProvider? GetProvider(string providerId);

    /// <summary>
    /// Get all registered Providers
    /// </summary>
    /// <returns>Provider list</returns>
    IReadOnlyList<IAIProvider> GetAllProviders();

    /// <summary>
    /// Get metadata information of all Providers
    /// </summary>
    /// <returns>Provider information list</returns>
    IReadOnlyList<AIProviderInfo> GetAllProviderInfos();

    /// <summary>
    /// Get the default Provider
    /// </summary>
    /// <returns>Default Provider instance</returns>
    IAIProvider? GetDefaultProvider();

    /// <summary>
    /// Check whether the specified Provider has been registered
    /// </summary>
    /// <param name="providerId">Provider ID</param>
    /// <returns>Have you registered?</returns>
    bool HasProvider(string providerId);
}
