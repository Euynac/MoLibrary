using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Repository;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Entity.Services;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Services;
using Monica.Repository.Persistence.Services.Support;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleRepositoryBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the Repository module
        /// </summary>
        public static ModuleRepositoryGuide AddRepository(Action<ModuleRepositoryOption>? action = null)
        {
            return new ModuleRepositoryGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.Repository)]
public class ModuleRepository(ModuleRepositoryOption option)
    : MoModule<ModuleRepository, ModuleRepositoryOption, ModuleRepositoryGuide>(option)
{

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleObjectMappingGuide>().Register();
    }
}

public class ModuleRepositoryGuide : MoModuleGuide<ModuleRepository, ModuleRepositoryOption, ModuleRepositoryGuide>
{

    public ModuleRepositoryGuide AddRepositoryDbContext<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> optionsAction, DbContextProviderType dbContextProviderType = DbContextProviderType.Default)
        where TDbContext : RepositoryDbContext<TDbContext>
    {
        if (dbContextProviderType == DbContextProviderType.UnitOfWork)
        {
            DependsOnModule<ModuleUnitOfWorkGuide>().Register().AddDbContextProvider<TDbContext>();
        }

        ConfigureServices(context =>
        {
            switch (dbContextProviderType)
            {
                case DbContextProviderType.ContextFactory:
                    // Register EF Core factory and wrap it with our provider interface
                    context.Services.AddDbContextFactory<TDbContext>(optionsAction);
                    context.Services.AddSingleton(
                        typeof(IDbContextProvider<TDbContext>),
                        typeof(DbContextFactoryProvider<TDbContext>));
                    break;
                case DbContextProviderType.Default:
                    context.Services.AddTransient(typeof(IDbContextProvider<TDbContext>), typeof(DefaultDbContextProvider<TDbContext>));
                    break;
            }
            
            context.Services.TryAddTransient<IAuditPropertySetter, AuditPropertySetter>();

            // Only register factory separately if not using ContextFactory provider type
            if (context.ModuleOption.UseDbContextFactory && dbContextProviderType != DbContextProviderType.ContextFactory)
            {
                context.Services.AddDbContextFactory<TDbContext>(optionsAction);
            }

            // Only register DbContext if not using ContextFactory (factory pattern doesn't need scoped DbContext)
            if (dbContextProviderType != DbContextProviderType.ContextFactory)
            {
                context.Services.AddDbContext<TDbContext>(optionsAction);
            }

            //TODO Use Module to optimize automatic registration
            var options = new EfRepositoryRegistrationOptions(typeof(TDbContext), context.Services);

            context.Services.AddTransient(serviceProvider =>
            {
                var builder = new DbContextOptionsBuilder<TDbContext>()
                    .UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>())
                    .UseApplicationServiceProvider(serviceProvider);
                optionsAction?.Invoke(serviceProvider, builder);
                return builder.Options;
            });

            new EfCoreRepositoryRegistrar(options).AddRepositories();

            context.Services
                .AddTransient<IDbContextDatabaseManager<TDbContext>, DbContextDatabaseManager<TDbContext>>();
        }, secondKey: typeof(TDbContext).Name);
        return this;
    }
}

public class ModuleRepositoryOption : MoModuleOption<ModuleRepository>
{
    /// <summary>
    /// Use User-defined function mapping to filter data.
    /// https://learn.microsoft.com/en-us/ef/core/querying/user-defined-function-mapping
    /// </summary>
    public bool UseDbFunction { get; set; }

    /// <summary>
    /// Automatically register DbContext Factory
    /// </summary>
    public bool UseDbContextFactory { get; set; }

    /// <summary>
    /// Whether to enable sensitive data logging. The default is null, which means it is enabled when the environment is Development.
    /// </summary>
    public bool? EnableSensitiveDataLogging { get; set; }

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
    /// Maximum length of concurrent tokens
    /// </summary>
    public static int ConcurrencyStampMaxLength = 40;
}

public enum DbContextProviderType
{
    Default,
    UnitOfWork,
    ContextFactory
}
