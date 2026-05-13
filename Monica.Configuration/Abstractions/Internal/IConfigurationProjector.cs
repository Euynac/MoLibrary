using Monica.Configuration.Models;
using Monica.Configuration.Models.Internal;

namespace Monica.Configuration.Abstractions.Internal;

/// <summary>
/// Projects merged Monica values into flat Microsoft configuration keys.
/// </summary>
internal interface IConfigurationProjector
{
    /// <summary>
    /// Projects merged values.
    /// </summary>
    /// <param name="definitions">Known definitions.</param>
    /// <param name="values">Merged values.</param>
    /// <returns>Flat configuration keys.</returns>
    IReadOnlyList<ProjectedConfigurationKey> Project(IReadOnlyList<ConfigurationDefinition> definitions, IReadOnlyList<MergedNodeValue> values);
}
