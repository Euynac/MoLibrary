using Microsoft.Extensions.Configuration;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Basic bootstrap reader that binds from already projected flat values.
/// </summary>
internal sealed class ConfigurationBootstrapReader : IConfigurationBootstrapReader
{
    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string?>> ReadFlatValuesAsync(
        IReadOnlyList<ConfigurationDefinition> definitions,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?>());
    }

    /// <inheritdoc />
    public async Task<TOptions?> GetOptionsAsync<TOptions>(
        ConfigurationDefinition definition,
        CancellationToken cancellationToken)
        where TOptions : class
    {
        var values = await ReadFlatValuesAsync([definition], cancellationToken);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return configuration.GetSection(definition.SectionPath).Get<TOptions>();
    }
}
