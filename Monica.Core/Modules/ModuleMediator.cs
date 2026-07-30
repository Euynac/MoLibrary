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

        var serviceTypes = implementationType
            .GetInterfaces()
            .Where(static serviceType => serviceType.IsGenericType &&
                                         serviceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
            .ToArray();
        if (serviceTypes.Length == 0)
        {
            return;
        }

        if (implementationType.IsGenericTypeDefinition)
        {
            foreach (var serviceType in serviceTypes)
            {
                services.TryAddTransient(serviceType.GetGenericTypeDefinition(), implementationType);
            }

            return;
        }

        // Keep one canonical activation path for closed handlers. Other modules may enrich or replace the concrete
        // registration, while the mediator contract remains an alias to that final registration.
        services.TryAddTransient(implementationType);
        foreach (var serviceType in serviceTypes)
        {
            services.TryAddTransient(
                serviceType,
                provider => provider.GetRequiredService(implementationType));
        }
    }
}

public class ModuleMediatorGuide : ModuleGuide<ModuleMediator, ModuleMediatorOption, ModuleMediatorGuide>;

public class ModuleMediatorOption : ModuleOptions<ModuleMediator>;
