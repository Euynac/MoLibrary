using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Modules;
using MoLibrary.DependencyInjection.DynamicProxy;
using MoLibrary.DependencyInjection.DynamicProxy.DefaultInterceptors;
using MoLibrary.Repository.EntityInterfaces;
using MoLibrary.Repository.Interfaces;
using MoLibrary.Repository.Registrar;
using MoLibrary.Repository.Transaction;
using MoLibrary.Tool.Extensions;
// ReSharper disable ExplicitCallerInfoArgument

namespace MoLibrary.Repository.Modules;

public enum DbContextProviderType
{
    Default,
    UnitOfWork,
    ContextFactory
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