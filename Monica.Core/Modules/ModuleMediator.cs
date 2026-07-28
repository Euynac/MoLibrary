using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Mediator;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleMediatorBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the Mediator module.
        /// </summary>
        public ModuleMediatorGuide AddMediator(Action<ModuleMediatorOption>? action = null)
        {
            return builder.AddModule<ModuleMediator, ModuleMediatorOption, ModuleMediatorGuide>(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.Mediator)]
public class ModuleMediator(ModuleMediatorOption option)
    : ModuleBase<ModuleMediator, ModuleMediatorOption, ModuleMediatorGuide>(option), IBusinessTypeIterator
{
    private IServiceCollection? _services;

    public override void ConfigureServices(IServiceCollection services)
    {
        _services = services;
        services.TryAddTransient<IMediator, Mediator>();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleExecutionPipelineGuide>().Register();
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        if (_services == null)
        {
            foreach (var type in types)
            {
                yield return type;
            }

            yield break;
        }

        foreach (var type in types)
        {
            RegisterRequestHandlers(_services, type);
            yield return type;
        }
    }

    private static void RegisterRequestHandlers(IServiceCollection services, Type implementationType)
    {
        if (implementationType is not { IsClass: true, IsAbstract: false })
        {
            return;
        }

        foreach (var serviceType in implementationType
                     .GetInterfaces()
                     .Where(static serviceType => serviceType.IsGenericType &&
                                                  serviceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
        {
            if (implementationType.IsGenericTypeDefinition)
            {
                services.TryAddTransient(serviceType.GetGenericTypeDefinition(), implementationType);
                continue;
            }

            services.TryAddTransient(serviceType, implementationType);
        }
    }
}

public class ModuleMediatorGuide : ModuleGuide<ModuleMediator, ModuleMediatorOption, ModuleMediatorGuide>;

public class ModuleMediatorOption : ModuleOptions<ModuleMediator>;
