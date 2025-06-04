using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.DependencyInjection.DynamicProxy;
using MoLibrary.DependencyInjection.DynamicProxy.DefaultInterceptors;
using MoLibrary.Repository.EntityInterfaces;
using MoLibrary.Repository.Interfaces;
using MoLibrary.Repository.Registrar;
using MoLibrary.Repository.Transaction;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Repository.Modules;

public class ModuleRepositoryGuide : MoModuleGuide<ModuleRepository, ModuleRepositoryOption, ModuleRepositoryGuide>
{
    //protected override string[] GetRequestedConfigMethodKeys()
    //{
    //    return [ADD_DB_CONTEXT_PROVIDER, nameof(AddMoDbContext)];
    //}
    protected const string ADD_DB_CONTEXT_PROVIDER = nameof(ADD_DB_CONTEXT_PROVIDER);
    public ModuleRepositoryGuide AddMoUnitOfWorkDbContextProvider(bool addEventSupport = false)
    {
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
        }, key: ADD_DB_CONTEXT_PROVIDER);
        return this;
    }

    /// <summary>
    /// Adds a service of type <see cref="IDbContextProvider{TDbContext}"/> with the implementation type of <see cref="DefaultDbContextProvider{T}"/> to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>You need to manually save changes.</remarks>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public ModuleRepositoryGuide AddMoDefaultDbContextProvider()
    {
        ConfigureServices(context =>
        {
            context.Services.AddScoped(typeof(IDbContextProvider<>), typeof(DefaultDbContextProvider<>));
        }, key: ADD_DB_CONTEXT_PROVIDER);
        return this;
    }

    public ModuleRepositoryGuide AddMoDbContext<TDbContext>(Action<IServiceProvider, DbContextOptionsBuilder> optionsAction, Action<ModuleRepositoryOption>? moOptionsAction = null)
        where TDbContext : MoDbContext<TDbContext>
    {
        ConfigureServices(context =>
        {
            context.Services.AddMemoryCache();

            context.Services.AddTransient(typeof(IDbContextProvider<>), typeof(UnitOfWorkDbContextProvider<>));


            if (context.ModuleOption.UseDbContextFactory)
            {
                context.Services.AddDbContextFactory<TDbContext>(optionsAction);
            }

            context.Services.AddDbContext<TDbContext>(optionsAction);

            //TODO 使用Module优化自动注册
            var options = new MoEfCoreRegistrationOptions(typeof(TDbContext), context.Services);

            context.Services.AddTransient<IMoAuditPropertySetter, MoAuditPropertySetter>();

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
                context =>
                {
                    var type = context.ImplementationType;
                    if (type.IsAssignableTo<IMoRepository>())
                    {
                        //GlobalLog.LogInformation("property injection: {service}", type.GetGenericTypeName());
                        return true;
                    }

                    return false;
                });
        });
        return this;
    }
}