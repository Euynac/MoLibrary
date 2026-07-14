using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Repository;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Entity.Services;
using Monica.Repository.Facades;
using Monica.Repository.GuidGeneration.Abstractions;
using Monica.Repository.GuidGeneration.Models;
using Monica.Repository.GuidGeneration.Services;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Metrics;
using Monica.Repository.Persistence.Models;
using Monica.Repository.Persistence.Services;
using Monica.Repository.Persistence.Services.Support;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleRepositoryBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the Repository module
        /// </summary>
        public ModuleRepositoryGuide AddRepository(Action<ModuleRepositoryOption>? action = null)
        {
            return builder.AddModule<ModuleRepository, ModuleRepositoryOption, ModuleRepositoryGuide>(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.Repository)]
public class ModuleRepository(ModuleRepositoryOption option)
    : ModuleBase<ModuleRepository, ModuleRepositoryOption, ModuleRepositoryGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions<SequentialGuidGeneratorOptions>();
        services.TryAddTransient<IGuidGenerator, SequentialGuidGenerator>();
        services.TryAddSingleton<IRepositoryDbContextRegistry, RepositoryDbContextRegistry>();
        services.TryAddScoped<IRepositoryDbContextDiagnosticsService, RepositoryDbContextDiagnosticsService>();
        services.TryAddScoped<RepositoryDiagnosticsFacade>();

        if (option.EnableEfCoreConnectionMetrics)
        {
            services.TryAddSingleton<EfCoreConnectionMetrics>();
            services.TryAddSingleton<EfCoreConnectionMetricsInterceptor>();
        }
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleObjectMappingGuide>().Register();
    }
}

public class ModuleRepositoryGuide : ModuleGuide<ModuleRepository, ModuleRepositoryOption, ModuleRepositoryGuide>
{
    /// <summary>
    /// Registers a repository DbContext and the scoped access services used by repositories and long-lived workers.
    /// </summary>
    /// <typeparam name="TDbContext">The repository DbContext type to register.</typeparam>
    /// <param name="optionsAction">Configures the EF Core provider and options for the DbContext.</param>
    /// <param name="dbContextProviderType">Selects how scoped repositories obtain the current DbContext.</param>
    /// <returns>The repository guide for method chaining.</returns>
    public ModuleRepositoryGuide AddRepositoryDbContext<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> optionsAction, DbContextProviderType dbContextProviderType = DbContextProviderType.Default)
        where TDbContext : RepositoryDbContext<TDbContext>
    {
        if (dbContextProviderType == DbContextProviderType.UnitOfWork)
        {
            DependsOnModule<ModuleUnitOfWorkGuide>().Register().AddDbContextProvider<TDbContext>();
        }
        else if (dbContextProviderType != DbContextProviderType.Default)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dbContextProviderType),
                dbContextProviderType,
                "Unknown repository DbContext provider type.");
        }

        ConfigureServices(context =>
        {
            if (dbContextProviderType == DbContextProviderType.Default)
            {
                context.Services.AddTransient(typeof(IDbContextProvider<TDbContext>), typeof(DefaultDbContextProvider<TDbContext>));
            }
            
            context.Services.TryAddTransient<IAuditPropertySetter, AuditPropertySetter>();
            context.Services.AddSingleton(new RepositoryDbContextRegistration(
                typeof(TDbContext),
                dbContextProviderType));
            context.Services.TryAddSingleton(
                typeof(IDbContextOperation<TDbContext>),
                typeof(ScopedDbContextOperation<TDbContext>));
            context.Services.AddDbContext<TDbContext>(
                (serviceProvider, builder) => ConfigureDbContextOptions(context.ModuleOption, serviceProvider, builder, optionsAction));

            //TODO Use Module to optimize automatic registration
            var options = new EfRepositoryRegistrationOptions(typeof(TDbContext), context.Services);

            context.Services.AddTransient(serviceProvider =>
            {
                var builder = new DbContextOptionsBuilder<TDbContext>()
                    .UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>())
                    .UseApplicationServiceProvider(serviceProvider);
                ConfigureDbContextOptions(context.ModuleOption, serviceProvider, builder, optionsAction);
                return builder.Options;
            });

            new EfCoreRepositoryRegistrar(options).AddRepositories();

            context.Services
                .AddTransient<IDbContextDatabaseManager<TDbContext>, DbContextDatabaseManager<TDbContext>>();
        }, secondKey: typeof(TDbContext).FullName ?? typeof(TDbContext).Name);
        return this;
    }

    private static void ConfigureDbContextOptions(
        ModuleRepositoryOption option,
        IServiceProvider serviceProvider,
        DbContextOptionsBuilder builder,
        Action<IServiceProvider, DbContextOptionsBuilder> optionsAction)
    {
        optionsAction(serviceProvider, builder);

        if (option.EnableEfCoreConnectionMetrics)
        {
            builder.AddInterceptors(serviceProvider.GetRequiredService<EfCoreConnectionMetricsInterceptor>());
        }
    }
}

public class ModuleRepositoryOption : ModuleOptions<ModuleRepository>
{
    /// <summary>
    /// Use User-defined function mapping to filter data.
    /// https://learn.microsoft.com/en-us/ef/core/querying/user-defined-function-mapping
    /// </summary>
    public bool UseDbFunction { get; set; }

    /// <summary>
    /// Gets or sets whether EF Core includes parameter values in diagnostic output.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="null"/>, which enables sensitive data logging only when the current host's
    /// <see cref="Microsoft.Extensions.Hosting.IHostEnvironment"/> is Development. Set an explicit value when
    /// repository diagnostics must not depend on the host environment.
    /// </remarks>
    public bool? EnableSensitiveDataLogging { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Repository should emit EF Core connection lifecycle metrics.
    /// </summary>
    /// <remarks>
    /// Enable this when a host wants to collect Repository persistence metrics through <c>System.Diagnostics.Metrics</c>
    /// and an OpenTelemetry-compatible exporter. Monica emits the metrics but does not configure any exporter.
    /// </remarks>
    public bool EnableEfCoreConnectionMetrics { get; set; }

    /// <summary>
    /// Disable the entity <see cref="IHasEntitySelfConfig{TEntity}"/> function, which can be turned off when not in use
    /// </summary>
    public bool DisableEntitySelfConfiguration { get; set; }

    /// <summary>
    /// Disables automatic discovery of entity-specific configuration via <see cref="IEntityTypeConfiguration{TEntity}"/>.
    /// You can disable this when automatic registration from <see cref="RepositoryDbContext{TDbContext}"/> is not needed.
    /// </summary>
    public bool DisableEntitySeparateConfiguration { get; set; }

    /// <summary>
    /// Maximum length used for concurrency-stamp columns configured by Repository conventions.
    /// </summary>
    public const int ConcurrencyStampMaxLength = 40;
}

/// <summary>
/// Selects how scoped repository services resolve the DbContext for a request or operation.
/// </summary>
public enum DbContextProviderType
{
    /// <summary>
    /// Resolves the current DbContext directly from the active dependency injection scope.
    /// </summary>
    Default,

    /// <summary>
    /// Resolves the current DbContext from the active unit of work.
    /// </summary>
    UnitOfWork
}
