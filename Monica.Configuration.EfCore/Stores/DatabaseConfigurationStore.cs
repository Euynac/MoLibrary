using Monica.Configuration.EfCore.Stores.Support;
using Monica.Modules;

namespace Monica.Configuration.EfCore.Stores;

/// <summary>
/// Provides the administrative entry point for the EF Core configuration store bundle.
/// </summary>
/// <remarks>
/// Resolve this type from dependency injection when schema creation is managed explicitly. Runtime configuration
/// capabilities are exposed through the focused store interfaces registered by <see cref="ModuleConfigurationEfCore"/>.
/// </remarks>
public sealed class DatabaseConfigurationStore
{
    private readonly ConfigurationDatabaseSchemaManager _schemaManager;

    internal DatabaseConfigurationStore(ConfigurationDatabaseSchemaManager schemaManager)
    {
        _schemaManager = schemaManager;
    }

    /// <summary>
    /// Creates the Monica.Configuration database schema required by this runtime when no earlier schema exists.
    /// </summary>
    /// <remarks>
    /// Hosts that disable <see cref="ModuleConfigurationEfCoreOption.AutoManageSchema"/> should call this method from
    /// an explicit deployment or startup initialization step before configuration stores are used. The operation is
    /// idempotent for a current schema. Earlier schema versions are rejected because version 8 requires a fresh database.
    /// </remarks>
    /// <param name="cancellationToken">The token that cancels schema validation or creation.</param>
    /// <returns>A task that completes when the required schema is ready.</returns>
    public Task InitializeSchemaAsync(CancellationToken cancellationToken = default)
    {
        return _schemaManager.InitializeAsync(cancellationToken);
    }
}
