using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Applies mutations to supported external Microsoft configuration sources.
/// </summary>
public interface IConfigurationSourceMutationService
{
    /// <summary>
    /// Mutates one external source value.
    /// </summary>
    /// <param name="request">The source mutation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The mutation result.</returns>
    Task<ConfigurationMutationResult> MutateAsync(ConfigurationSourceMutationRequest request, CancellationToken cancellationToken);
}
