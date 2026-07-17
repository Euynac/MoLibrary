using Microsoft.EntityFrameworkCore;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Repository.Persistence.Abstractions;

namespace Monica.Configuration.EfCore.Stores.Support;

/// <summary>
/// Provides the shared execution boundary for relational configuration stores.
/// </summary>
internal sealed class ConfigurationDatabase(
    IDbContextOperation<ConfigurationDbContext> dbContextOperation)
{
    private const string SCHEMA_MIGRATION_REQUIRED_MESSAGE =
        "The Monica.Configuration database schema is missing or incompatible. Apply the host-owned EF Core migrations "
        + "for ConfigurationDbContext before using the configuration store.";

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
        return await TranslateSchemaFailureAsync(
            () => dbContextOperation.ExecuteAsync(operation, cancellationToken));
    }

    internal async Task ExecuteAsync(
        Func<ConfigurationDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        await TranslateSchemaFailureAsync(
            () => dbContextOperation.ExecuteAsync(operation, cancellationToken));
    }

    internal async Task<TResult> ExecuteResilientAsync<TResult>(
        Func<ConfigurationDbContext, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        return await TranslateSchemaFailureAsync(() =>
            dbContextOperation.ExecuteAsync(async (strategyContext, token) =>
            {
                var strategy = strategyContext.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                    await dbContextOperation.ExecuteAsync(operation, token));
            }, cancellationToken));
    }

    private static async Task<TResult> TranslateSchemaFailureAsync<TResult>(Func<Task<TResult>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (ex is not ConfigurationStoreSchemaException
                                   && ConfigurationDatabaseExceptionClassifier.IsMissingSchemaObject(ex))
        {
            throw new ConfigurationStoreSchemaException(SCHEMA_MIGRATION_REQUIRED_MESSAGE, ex);
        }
    }

    private static async Task TranslateSchemaFailureAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception ex) when (ex is not ConfigurationStoreSchemaException
                                   && ConfigurationDatabaseExceptionClassifier.IsMissingSchemaObject(ex))
        {
            throw new ConfigurationStoreSchemaException(SCHEMA_MIGRATION_REQUIRED_MESSAGE, ex);
        }
    }
}
