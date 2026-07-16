using Monica.Configuration.EfCore.Stores.Support;
using Monica.Modules;

namespace Monica.Configuration.EfCore.Stores;

/// <summary>
/// Provides the administrative entry point for the EF Core configuration store bundle.
/// </summary>
/// <remarks>
/// Resolve this type from dependency injection when schema upgrades are managed explicitly. Runtime configuration
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
    /// Creates or upgrades the Monica.Configuration database schema to the version required by this runtime.
    /// </summary>
    /// <remarks>
    /// Hosts that disable <see cref="ModuleConfigurationEfCoreOption.AutoManageSchema"/> should call this method from
    /// an explicit deployment or startup migration step before configuration stores are used. The operation is
    /// idempotent and applies only Monica.Configuration-owned schema changes and identity backfills.
    /// </remarks>
    /// <param name="cancellationToken">The token that cancels schema creation or upgrade.</param>
    /// <returns>A task that completes when the required schema is ready.</returns>
    public Task UpgradeSchemaAsync(CancellationToken cancellationToken = default)
    {
        return _schemaManager.UpgradeAsync(cancellationToken);
    }
}
