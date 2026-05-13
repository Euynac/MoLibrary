using Microsoft.Extensions.Configuration;
using Monica.Configuration.Models;
using Monica.Configuration.Providers;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Reads bootstrap values from environment variables.
/// </summary>
internal sealed class BootstrapEnvironmentReader
{
    /// <summary>
    /// Reads flat values for the supplied definitions.
    /// </summary>
    /// <param name="definitions">Configuration definitions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Flat Microsoft configuration values.</returns>
    public Task<IReadOnlyDictionary<string, string?>> ReadAsync(
        IReadOnlyList<ConfigurationDefinition> definitions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        return Task.FromResult<IReadOnlyDictionary<string, string?>>(definitions
            .SelectMany(definition => ConfigurationSourceNodeEnumerator
                .EnumerateLeaves(definition)
                .Select(leaf => new { leaf.ConfigurationPath, Value = configuration[leaf.ConfigurationPath] }))
            .Where(item => item.Value is not null)
            .ToDictionary(item => item.ConfigurationPath, item => item.Value, StringComparer.OrdinalIgnoreCase));
    }
}
