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
    UnitOfWork
}

public class ModuleRepositoryGuide : MoModuleGuide<ModuleRepository, ModuleRepositoryOption, ModuleRepositoryGuide>
{
    public ModuleRepositoryGuide AddMoUnitOfWorkSupport(bool addEventSupport = false)
    {
        DependsOnModule<ModuleScopedDataGuide>().Register()
            .AddKeyedScopedData<MoScopedDataUnitOfWorkProvider>(nameof(ModuleRepository));
        ConfigureServices(context =>
        {
            if (addEventSupport)
            {
                context.Services.AddMoUnitOfWorkWithEvent();
            }
            else
            {
                context.Services.AddMoUnitOfWork();
            }
        });
        return this;
    }

    public ModuleRepositoryGuide AddMoDbContext<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> optionsAction, DbContextProviderType dbContextProviderType = DbContextProviderType.Default)
        where TDbContext : MoDbContext<TDbContext>
    {
        ConfigureServices(context =>
        {
            switch (dbContextProviderType)
            {
                case DbContextProviderType.UnitOfWork:
                    CheckRequiredMethod(nameof(AddMoUnitOfWorkSupport));
                    context.Services.AddTransient(typeof(IDbContextProvider<TDbContext>), typeof(UnitOfWorkDbContextProvider<TDbContext>));
                    //TODO 可使用Singleton？
                    break;
                case DbContextProviderType.Default:
                    context.Services.AddTransient(typeof(IDbContextProvider<TDbContext>), typeof(DefaultDbContextProvider<TDbContext>));
                    break;
            }
            
            context.Services.TryAddTransient<IMoAuditPropertySetter, MoAuditPropertySetter>();
            if (context.ModuleOption.UseDbContextFactory)
            {
                context.Services.AddDbContextFactory<TDbContext>(optionsAction);
            }

            context.Services.AddDbContext<TDbContext>(optionsAction);

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