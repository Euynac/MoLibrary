using System.Collections.Concurrent;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services;

/// <summary>
/// In-process registry of configuration definitions known to this service.
/// </summary>
internal sealed class ConfigurationDefinitionRegistry : IConfigurationDefinitionRegistry
{
    private readonly ConcurrentDictionary<string, ConfigurationDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(ConfigurationDefinition definition)
    {
        _definitions[definition.DefinitionKey] = definition;
    }

    /// <inheritdoc />
    public IReadOnlyList<ConfigurationDefinition> GetAll()
    {
        return [.. _definitions.Values.OrderBy(x => x.DefinitionKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc />
    public ConfigurationDefinition GetRequired(string definitionKey)
    {
        return TryGet(definitionKey, out var definition)
            ? definition!
            : throw new ConfigurationDefinitionNotFoundException(definitionKey);
    }

    /// <inheritdoc />
    public bool TryGet(string definitionKey, out ConfigurationDefinition? definition)
    {
        return _definitions.TryGetValue(definitionKey, out definition);
    }
}
