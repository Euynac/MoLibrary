using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Services;
using Monica.Testing.Repository;

namespace Monica.Testing.Hosting;

/// <summary>
/// Database isolation strategy used by sociable application tests.
/// </summary>
public enum DatabaseIsolation
{
    /// <summary>
    /// Creates a fresh SQLite in-memory database for each test scope.
    /// </summary>
    PerScopeDatabase,

    /// <summary>
    /// Uses one SQLite in-memory database for the fixture and rolls back a transaction per scope.
    /// </summary>
    SharedDatabaseWithTransaction,

    /// <summary>
    /// Uses caller-supplied provider configuration. The toolkit does not create or own the database.
    /// </summary>
    RealDatabase
}

/// <summary>
/// Registers test database seams.
/// </summary>
public static class TestDatabaseServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TDbContext"/> with a SQLite in-memory database for sociable tests.
    /// </summary>
    public static IServiceCollection UseTestDatabase<TDbContext>(
        this IServiceCollection services,
        DatabaseIsolation isolation = DatabaseIsolation.PerScopeDatabase)
        where TDbContext : RepositoryDbContext<TDbContext>
    {
        if (isolation == DatabaseIsolation.RealDatabase)
        {
            throw new NotSupportedException(
                "Real database tests require UseRealTestDatabase<TDbContext>() with explicit provider configuration.");
        }

        services.TryAddSingleton<TestDbContextTypeRegistry>();
        services.Configure<TestDbContextTypeRegistryOptions>(options => options.Add<TDbContext>());
        services.RemoveAll<TDbContext>();
        services.RemoveAll<IDbContextProvider<TDbContext>>();

        if (isolation == DatabaseIsolation.SharedDatabaseWithTransaction)
        {
            services.AddSingleton<SharedSqliteDatabase<TDbContext>>();
            services.AddScoped<TestDbContextTransaction<TDbContext>>();
        }
        else
        {
            services.AddScoped<TestSqliteConnection<TDbContext>>();
        }

        services.AddScoped<TDbContext>(sp =>
        {
            var connection = isolation == DatabaseIsolation.SharedDatabaseWithTransaction
                ? sp.GetRequiredService<SharedSqliteDatabase<TDbContext>>().Connection
                : sp.GetRequiredService<TestSqliteConnection<TDbContext>>().Connection;

            var options = new DbContextOptionsBuilder<TDbContext>()
                .UseSqlite(connection)
                .UseApplicationServiceProvider(sp)
                .Options;

            var dbContext = ActivatorUtilities.CreateInstance<TDbContext>(
                sp,
                options,
                sp.GetRequiredService<Monica.DependencyInjection.Abstractions.ICachedServiceProvider>());
            dbContext.Database.EnsureCreated();
            sp.GetService<TestDbContextTransaction<TDbContext>>()?.Attach(dbContext);
            return dbContext;
        });
        services.AddScoped<IDbContextProvider<TDbContext>, TestDbContextProvider<TDbContext>>();

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TDbContext"/> with caller-supplied provider configuration.
    /// </summary>
    public static IServiceCollection UseRealTestDatabase<TDbContext>(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder<TDbContext>> configureProvider)
        where TDbContext : RepositoryDbContext<TDbContext>
    {
        ArgumentNullException.ThrowIfNull(configureProvider);

        services.TryAddSingleton<TestDbContextTypeRegistry>();
        services.Configure<TestDbContextTypeRegistryOptions>(options => options.Add<TDbContext>());
        services.RemoveAll<TDbContext>();
        services.RemoveAll<IDbContextProvider<TDbContext>>();
        services.AddScoped<TDbContext>(sp =>
        {
            var builder = new DbContextOptionsBuilder<TDbContext>()
                .UseApplicationServiceProvider(sp);
            configureProvider(sp, builder);
            return ActivatorUtilities.CreateInstance<TDbContext>(
                sp,
                builder.Options,
                sp.GetRequiredService<Monica.DependencyInjection.Abstractions.ICachedServiceProvider>());
        });
        services.AddScoped<IDbContextProvider<TDbContext>, TestDbContextProvider<TDbContext>>();

        return services;
    }
}

internal sealed class TestSqliteConnection<TDbContext> : IDisposable
    where TDbContext : DbContext
{
    public TestSqliteConnection()
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        Connection.Open();
    }

    public SqliteConnection Connection { get; }

    public void Dispose()
    {
        Connection.Dispose();
    }
}

internal sealed class SharedSqliteDatabase<TDbContext> : IDisposable
    where TDbContext : DbContext
{
    public SharedSqliteDatabase()
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        Connection.Open();
    }

    public SqliteConnection Connection { get; }

    public void Dispose()
    {
        Connection.Dispose();
    }
}

internal sealed class TestDbContextTransaction<TDbContext> : IAsyncDisposable
    where TDbContext : DbContext
{
    private IDbContextTransaction? _transaction;

    public void Attach(TDbContext dbContext)
    {
        _transaction ??= dbContext.Database.BeginTransaction();
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
    }
}

/// <summary>
/// Tracks DbContext types registered in a sociable test scope.
/// </summary>
public sealed class TestDbContextTypeRegistryOptions
{
    private readonly HashSet<Type> _types = [];

    public IReadOnlyCollection<Type> Types => _types;

    public void Add<TDbContext>()
        where TDbContext : DbContext
    {
        _types.Add(typeof(TDbContext));
    }
}

/// <summary>
/// Exposes DbContext types registered in a sociable test scope.
/// </summary>
public sealed class TestDbContextTypeRegistry(Microsoft.Extensions.Options.IOptions<TestDbContextTypeRegistryOptions> options)
{
    public IReadOnlyCollection<Type> Types => options.Value.Types;
}
