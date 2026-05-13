using Monica.Configuration.Models;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Composes bootstrap source readers into one flat configuration map.
/// </summary>
internal sealed class BootstrapSourcePipeline(
    BootstrapJsonReader jsonReader,
    BootstrapEnvironmentReader environmentReader)
{
    /// <summary>
    /// Reads all bootstrap values. Later sources override earlier sources.
    /// </summary>
    /// <param name="definitions">Configuration definitions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Flat Microsoft configuration values.</returns>
    public async Task<IReadOnlyDictionary<string, string?>> ReadAsync(
        IReadOnlyList<ConfigurationDefinition> definitions,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceValues in new[]
                 {
                     await jsonReader.ReadAsync(definitions, cancellationToken),
                     await environmentReader.ReadAsync(definitions, cancellationToken)
                 })
        {
            foreach (var (key, value) in sourceValues)
            {
                values[key] = value;
            }
        }

        return values;
    }
}
