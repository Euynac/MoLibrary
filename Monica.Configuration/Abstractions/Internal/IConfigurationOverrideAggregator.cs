using Monica.Configuration.Models.Internal;

namespace Monica.Configuration.Abstractions.Internal;

/// <summary>
/// Loads normalized overrides from all registered value sources.
/// </summary>
internal interface IConfigurationOverrideAggregator
{
    /// <summary>
    /// Loads overrides.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Normalized override sets by source.</returns>
    Task<IReadOnlyList<NormalizedOverrideSet>> LoadAsync(CancellationToken cancellationToken);
}
