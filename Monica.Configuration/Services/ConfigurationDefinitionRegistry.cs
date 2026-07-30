using Monica.Configuration.Abstractions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services;

/// <summary>
/// In-process registry of configuration definitions known to this service.
/// </summary>
internal sealed class ConfigurationDefinitionRegistry : IConfigurationDefinitionRegistry
{
    private readonly object _gate = new();
    private Dictionary<string, ConfigurationDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(ConfigurationDefinition definition)
    {
        RegisterRange([definition]);
    }

    /// <summary>
    /// Atomically publishes a complete definition batch so readers never observe a partially scanned host catalog.
    /// </summary>
    public void RegisterRange(IEnumerable<ConfigurationDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var additions = definitions.ToArray();
        if (additions.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            var updated = new Dictionary<string, ConfigurationDefinition>(
                Volatile.Read(ref _definitions),
                StringComparer.OrdinalIgnoreCase);
            foreach (var definition in additions)
            {
                ArgumentNullException.ThrowIfNull(definition);
                updated[definition.DefinitionKey] = definition;
            }

            Volatile.Write(ref _definitions, updated);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ConfigurationDefinition> GetAll()
    {
        return [.. Volatile.Read(ref _definitions).Values.OrderBy(x => x.DefinitionKey, StringComparer.OrdinalIgnoreCase)];
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
        return Volatile.Read(ref _definitions).TryGetValue(definitionKey, out definition);
    }
}
