using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Applies runtime configuration mutations.
/// </summary>
public interface IConfigurationMutationService
{
    /// <summary>
    /// Mutates one configuration value or subtree.
    /// </summary>
    /// <param name="request">The mutation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The mutation result.</returns>
    Task<ConfigurationMutationResult> MutateAsync(ConfigurationMutationRequest request, CancellationToken cancellationToken);
}
