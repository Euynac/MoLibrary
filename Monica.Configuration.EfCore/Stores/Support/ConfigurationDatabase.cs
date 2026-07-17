using Microsoft.EntityFrameworkCore;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.Models;
using Monica.Repository.Persistence.Abstractions;

namespace Monica.Configuration.EfCore.Stores.Support;

/// <summary>
/// Provides the shared schema-ready execution boundary for relational configuration stores.
/// </summary>
internal sealed class ConfigurationDatabase(
    IDbContextOperation<ConfigurationDbContext> dbContextOperation,
    ConfigurationDatabaseSchemaManager schemaManager)
{
    internal static ConfigurationStoreDescriptor Descriptor { get; } = new()
    {
        StoreKey = "db:default",
        DisplayName = "Database",
        Kind = ConfigurationStoreKind.Database,
        SupportsEffectiveValues = true,
        SupportsHistory = true,
        SupportsMetadata = true
    };

    internal async Task<TResult> ExecuteAsync<TResult>(
        Func<ConfigurationDbContext, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await schemaManager.EnsureReadyAsync(cancellationToken);
        return await dbContextOperation.ExecuteAsync(operation, cancellationToken);
    }

    internal async Task ExecuteAsync(
        Func<ConfigurationDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        await schemaManager.EnsureReadyAsync(cancellationToken);
        await dbContextOperation.ExecuteAsync(operation, cancellationToken);
    }

    internal async Task<TResult> ExecuteResilientAsync<TResult>(
        Func<ConfigurationDbContext, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await schemaManager.EnsureReadyAsync(cancellationToken);
        return await dbContextOperation.ExecuteAsync(async (strategyContext, token) =>
        {
            var strategy = strategyContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
                await dbContextOperation.ExecuteAsync(operation, token));
        }, cancellationToken);
    }
}
