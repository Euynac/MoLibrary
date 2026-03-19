using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DependencyInjection.DynamicProxy;
using Monica.DependencyInjection.DynamicProxy.DefaultInterceptors;
using Monica.Repository;
using Monica.Repository.EntityInterfaces;
using Monica.Repository.Interfaces;
using Monica.Repository.Registrar;
using Monica.Repository.Transaction;
using Monica.Tool.Extensions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleRepositoryBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 Repository 模块
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
        DependsOnModule<ModuleMapperGuide>().Register();
    }
}

public class ModuleRepositoryGuide : MoModuleGuide<ModuleRepository, ModuleRepositoryOption, ModuleRepositoryGuide>
{

    public ModuleRepositoryGuide AddMoDbContext<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> optionsAction, DbContextProviderType dbContextProviderType = DbContextProviderType.Default)
        where TDbContext : MoDbContext<TDbContext>
    {
        if (dbContextProviderType == DbContextProviderType.UnitOfWork)
        {
            DependsOnModule<ModuleUnitOfWorkGuide>().Register().AddDbContextProvider<TDbContext>();
            DependsOnModule<ModuleScopedDataGuide>().Register()
                .AddKeyedScopedData<MoScopedDataUnitOfWorkProvider>(nameof(ModuleRepository));
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
            
            context.Services.TryAddTransient<IMoAuditPropertySetter, MoAuditPropertySetter>();

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

            //TODO 使用Module优化自动注册
            var options = new MoEfCoreRegistrationOptions(typeof(TDbContext), context.Services);

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
                .AddTransient<IMoDbContextDatabaseManager<TDbContext>, MoDbContextDatabaseManager<TDbContext>>();

            //TODO 优化无需AOP
            context.Services.AddMoInterceptor<PropertyInjectServiceProviderEmptyInterceptor>().CreateProxyWhenSatisfy(
                proxyBuildContext =>
                {
                    var type = proxyBuildContext.ImplementationType;
                    if (type.IsAssignableTo<IMoRepository>())
                    {
                        //GlobalLog.LogInformation("property injection: {service}", type.GetGenericTypeName());
                        return true;
                    }

                    return false;
                });
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
    /// 自动注册DbContext Factory
    /// </summary>
    public bool UseDbContextFactory { get; set; }

    /// <summary>
    /// 是否启用敏感数据日志。默认为null，表示当环境为Development时启用。
    /// </summary>
    public bool? EnableSensitiveDataLogging { get; set; }

    /// <summary>
    /// 禁用实体 <see cref="IHasEntitySelfConfig{TEntity}"/> 功能，当不使用此功能时可关闭
    /// </summary>
    public bool DisableEntitySelfConfiguration { get; set; }

    /// <summary>
    /// 禁用自动发现实体原生配置 <see cref="IEntityTypeConfiguration{TEntity}"/> 功能，当不使用<see cref="MoDbContext{TDbContext}"/>提供的此接口自动注册功能可关闭
    /// </summary>
    public bool DisableEntitySeparateConfiguration { get; set; }

    /// <summary>
    /// 并发令牌最大长度
    /// </summary>
    public static int ConcurrencyStampMaxLength = 40;
}

public enum DbContextProviderType
{
    Default,
    UnitOfWork,
    ContextFactory
}
