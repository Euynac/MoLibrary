using Microsoft.Extensions.Configuration;
using Monica.Configuration.Models;
using Monica.Configuration.Providers;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Reads bootstrap values from conventional appsettings JSON files.
/// </summary>
internal sealed class BootstrapJsonReader
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
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                          ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        if (!string.IsNullOrWhiteSpace(environment))
        {
            builder.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);
        }

        return Task.FromResult(ReadDefinitions(definitions, builder.Build()));
    }

    private static IReadOnlyDictionary<string, string?> ReadDefinitions(
        IReadOnlyList<ConfigurationDefinition> definitions,
        IConfiguration configuration)
    {
        return definitions
            .SelectMany(definition => ConfigurationSourceNodeEnumerator
                .EnumerateLeaves(definition)
                .Select(leaf => new { leaf.ConfigurationPath, Value = configuration[leaf.ConfigurationPath] }))
            .Where(item => item.Value is not null)
            .ToDictionary(item => item.ConfigurationPath, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }
}
