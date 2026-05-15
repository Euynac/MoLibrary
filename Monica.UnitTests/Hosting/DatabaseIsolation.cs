using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Services;
using Monica.UnitTests.Repository;

namespace Monica.UnitTests.Hosting;

/// <summary>
/// Database isolation strategy used by sociable application tests.
/// </summary>
public enum DatabaseIsolation
{
    /// <summary>
    /// Creates a fresh SQLite in-memory database for each test scope.
    /// </summary>
    PerScopeDatabase
}

/// <summary>
/// Registers test database seams.
/// </summary>
public static class TestDatabaseServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TDbContext"/> with a per-scope SQLite in-memory database.
    /// </summary>
    public static IServiceCollection UseTestDatabase<TDbContext>(
        this IServiceCollection services,
        DatabaseIsolation isolation = DatabaseIsolation.PerScopeDatabase)
        where TDbContext : RepositoryDbContext<TDbContext>
    {
        if (isolation != DatabaseIsolation.PerScopeDatabase)
        {
            throw new NotSupportedException($"Database isolation mode {isolation} is not implemented yet.");
        }

        services.TryAddSingleton<TestDbContextTypeRegistry>();
        services.Configure<TestDbContextTypeRegistryOptions>(options => options.Add<TDbContext>());

        services.RemoveAll<TDbContext>();
        services.RemoveAll<IDbContextProvider<TDbContext>>();
        services.AddScoped<TestSqliteConnection<TDbContext>>();
        services.AddScoped<TDbContext>(sp =>
        {
            var connection = sp.GetRequiredService<TestSqliteConnection<TDbContext>>().Connection;
            var options = new DbContextOptionsBuilder<TDbContext>()
                .UseSqlite(connection)
                .UseApplicationServiceProvider(sp)
                .Options;

            var dbContext = ActivatorUtilities.CreateInstance<TDbContext>(
                sp,
                options,
                sp.GetRequiredService<Monica.DependencyInjection.Abstractions.ICachedServiceProvider>());
            dbContext.Database.EnsureCreated();
            return dbContext;
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

internal sealed class TestDbContextTypeRegistryOptions
{
    private readonly HashSet<Type> _types = [];

    public IReadOnlyCollection<Type> Types => _types;

    public void Add<TDbContext>()
        where TDbContext : DbContext
    {
        _types.Add(typeof(TDbContext));
    }
}

internal sealed class TestDbContextTypeRegistry(Microsoft.Extensions.Options.IOptions<TestDbContextTypeRegistryOptions> options)
{
    public IReadOnlyCollection<Type> Types => options.Value.Types;
}
