using Monica.AI.Models;

namespace Monica.AI.Abstractions;

/// <summary>
/// Factory interface for creating and managing AI provider instances.
/// </summary>
public interface IAIProviderFactory
{
    /// <summary>
    /// Gets the provider with the specified ID.
    /// </summary>
    /// <param name="providerId">The provider ID.</param>
    /// <returns>The provider instance, or <c>null</c> if it does not exist.</returns>
    IAIProvider? GetProvider(string providerId);

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    /// <returns>A list of registered providers.</returns>
    IReadOnlyList<IAIProvider> GetAllProviders();

    /// <summary>
    /// Gets metadata for all providers.
    /// </summary>
    /// <returns>A list of provider metadata.</returns>
    IReadOnlyList<AIProviderInfo> GetAllProviderInfos();

    /// <summary>
    /// Gets the default provider.
    /// </summary>
    /// <returns>The default provider instance.</returns>
    IAIProvider? GetDefaultProvider();

    /// <summary>
    /// Checks whether the specified provider is registered.
    /// </summary>
    /// <param name="providerId">The provider ID.</param>
    /// <returns><c>true</c> if the provider is registered; otherwise, <c>false</c>.</returns>
    bool HasProvider(string providerId);
}
