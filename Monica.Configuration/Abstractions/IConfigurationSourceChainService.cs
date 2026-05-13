using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Builds source-chain diagnostics for configuration values.
/// </summary>
public interface IConfigurationSourceChainService
{
    /// <summary>
    /// Gets the source chain for one logical path.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="logicalPath">The logical path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source chain.</returns>
    Task<ConfigurationSourceChain> GetSourceChainAsync(string definitionKey, LogicalPath logicalPath, CancellationToken cancellationToken);
}
