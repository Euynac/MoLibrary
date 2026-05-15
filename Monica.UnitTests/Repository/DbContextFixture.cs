using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Authority.Identity.Abstractions;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Services;
using Monica.Modules;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Services;
using Monica.UnitTests.Doubles;

namespace Monica.UnitTests.Repository;

/// <summary>
/// Creates a real Monica repository DbContext with deterministic test dependencies.
/// </summary>
/// <typeparam name="TDbContext">The repository DbContext type.</typeparam>
public sealed class DbContextFixture<TDbContext> : IDisposable, IAsyncDisposable
    where TDbContext : RepositoryDbContext<TDbContext>
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AsyncServiceScope _scope;
    private readonly SqliteConnection? _sqliteConnection;
    private bool _disposed;

    private DbContextFixture(
        DbContextFixtureMode mode,
        DbContextOptions<TDbContext> options,
        ServiceProvider serviceProvider,
        AsyncServiceScope scope,
        SqliteConnection? sqliteConnection)
    {
        Mode = mode;
        _serviceProvider = serviceProvider;
        _scope = scope;
        _sqliteConnection = sqliteConnection;
        Context = ActivatorUtilities.CreateInstance<TDbContext>(
            scope.ServiceProvider,
            options,
            scope.ServiceProvider.GetRequiredService<ICachedServiceProvider>());
        Provider = new TestDbContextProvider<TDbContext>(scope.ServiceProvider, Context);
        Services = scope.ServiceProvider;
    }

    /// <summary>
    /// Gets the fixture provider mode.
    /// </summary>
    public DbContextFixtureMode Mode { get; }

    /// <summary>
    /// Gets the DbContext instance owned by this fixture.
    /// </summary>
    public TDbContext Context { get; }

    /// <summary>
    /// Gets an IDbContextProvider wrapper over <see cref="Context"/>.
    /// </summary>
    public IDbContextProvider<TDbContext> Provider { get; }

    /// <summary>
    /// Gets the service provider used to construct the DbContext.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Creates a fixture backed by EF Core's in-memory provider.
    /// </summary>
    public static DbContextFixture<TDbContext> UseEfInMemory(
        string? databaseName = null,
        Action<IServiceCollection>? configureServices = null)
    {
        databaseName ??= $"monica-test-{Guid.NewGuid():N}";
        return Create(
            DbContextFixtureMode.EfInMemory,
            builder => builder.UseInMemoryDatabase(databaseName),
            configureServices);
    }

    /// <summary>
    /// Creates a fixture backed by SQLite in-memory storage.
    /// </summary>
    public static DbContextFixture<TDbContext> UseSqliteInMemory(
        Action<IServiceCollection>? configureServices = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        return Create(
            DbContextFixtureMode.SqliteInMemory,
            builder => builder.UseSqlite(connection),
            configureServices,
            connection);
    }

    /// <summary>
    /// Creates a fixture using caller-supplied provider options.
    /// </summary>
    public static DbContextFixture<TDbContext> UseOptions(
        Action<DbContextOptionsBuilder<TDbContext>> configureOptions,
        Action<IServiceCollection>? configureServices = null)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);
        return Create(DbContextFixtureMode.CustomOptions, configureOptions, configureServices);
    }

    /// <summary>
    /// Creates a fixture using a connection string and caller-supplied provider configuration.
    /// </summary>
    public static DbContextFixture<TDbContext> UseConnectionString(
        string connectionString,
        Action<DbContextOptionsBuilder<TDbContext>, string> configureProvider,
        Action<IServiceCollection>? configureServices = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(configureProvider);
        return UseOptions(builder => configureProvider(builder, connectionString), configureServices);
    }

    /// <summary>
    /// Ensures that the database schema exists.
    /// </summary>
    public async Task<DbContextFixture<TDbContext>> EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await Context.Database.EnsureCreatedAsync(cancellationToken);
        return this;
    }

    /// <summary>
    /// Seeds the DbContext with custom code.
    /// </summary>
    public DbContextFixture<TDbContext> Seed(Action<TDbContext> seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        seed(Context);
        Context.SaveChanges();
        return this;
    }

    /// <summary>
    /// Seeds entities into the DbContext and saves changes.
    /// </summary>
    public DbContextFixture<TDbContext> Seed(params object[] entities)
    {
        Context.AddRange(entities);
        Context.SaveChanges();
        return this;
    }

    /// <summary>
    /// Creates a production EF repository backed by this fixture's DbContext.
    /// </summary>
    public IRepository<TEntity> Repository<TEntity>()
        where TEntity : class, IEntity
    {
        return new EfRepository<TDbContext, TEntity>(Provider);
    }

    /// <summary>
    /// Creates a production EF repository backed by this fixture's DbContext.
    /// </summary>
    public IRepository<TEntity, TKey> Repository<TEntity, TKey>()
        where TEntity : class, IEntity<TKey>
    {
        return new EfRepository<TDbContext, TEntity, TKey>(Provider);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await Context.DisposeAsync();
        await _scope.DisposeAsync();
        await _serviceProvider.DisposeAsync();
        await (_sqliteConnection?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    private static DbContextFixture<TDbContext> Create(
        DbContextFixtureMode mode,
        Action<DbContextOptionsBuilder<TDbContext>> configureOptions,
        Action<IServiceCollection>? configureServices,
        SqliteConnection? sqliteConnection = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddSingleton<IOptions<ModuleRepositoryOption>>(Options.Create(new ModuleRepositoryOption()));
        services.AddSingleton<ICurrentUser, TestCurrentUser>();
        services.AddSingleton<IAuditPropertySetter, TestAuditPropertySetter>();
        services.AddScoped<ICachedServiceProvider, CachedServiceProvider>();
        configureServices?.Invoke(services);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        var scope = provider.CreateAsyncScope();
        var builder = new DbContextOptionsBuilder<TDbContext>()
            .UseLoggerFactory(NullLoggerFactory.Instance)
            .UseApplicationServiceProvider(provider);
        configureOptions(builder);

        return new DbContextFixture<TDbContext>(mode, builder.Options, provider, scope, sqliteConnection);
    }
}
