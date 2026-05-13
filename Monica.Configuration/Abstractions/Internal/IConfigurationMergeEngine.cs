using Monica.Configuration.Models.Internal;

namespace Monica.Configuration.Abstractions.Internal;

/// <summary>
/// Merges source override sets into effective values.
/// </summary>
internal interface IConfigurationMergeEngine
{
    /// <summary>
    /// Merges normalized source sets.
    /// </summary>
    /// <param name="sourceSets">The normalized source sets.</param>
    /// <returns>The merged values.</returns>
    IReadOnlyList<MergedNodeValue> Merge(IReadOnlyList<NormalizedOverrideSet> sourceSets);
}
