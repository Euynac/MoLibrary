using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions.Internal;

/// <summary>
/// Enforces container snapshot versus leaf override invariants within one source.
/// </summary>
internal interface IConfigurationOverrideNormalizer
{
    /// <summary>
    /// Normalizes one source's overrides.
    /// </summary>
    /// <param name="overrides">The source overrides.</param>
    /// <returns>The normalized overrides.</returns>
    IReadOnlyList<ConfigurationValueOverride> Normalize(IReadOnlyList<ConfigurationValueOverride> overrides);
}
