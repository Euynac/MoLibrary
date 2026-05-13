using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.EfCore.Sources;

/// <summary>
/// EF Core value source placeholder. Override persistence is implemented after the core merge path is stabilized.
/// </summary>
public sealed class DatabaseConfigurationValueSource : IConfigurationValueSource
{
    /// <inheritdoc />
    public ConfigurationSourceDescriptor Descriptor { get; } = new()
    {
        SourceKey = "db:default",
        DisplayName = "Database",
        Kind = ConfigurationSourceKind.Database,
        Priority = 200,
        IsWritable = true,
        SupportsHistory = true
    };

    /// <inheritdoc />
    public Task<IReadOnlyList<ConfigurationValueOverride>> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ConfigurationValueOverride>>([]);
    }

    /// <inheritdoc />
    public Task<ConfigurationValueOverride?> GetAsync(string definitionKey, LogicalPath logicalPath, CancellationToken cancellationToken)
    {
        return Task.FromResult<ConfigurationValueOverride?>(null);
    }

    /// <inheritdoc />
    public Task<ConfigurationMutationResult> MutateAsync(ConfigurationMutationRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Database value mutation is reserved for the EF Core implementation phase.");
    }
}
