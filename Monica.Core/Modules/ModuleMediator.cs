using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Mediator;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleMediatorBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the Mediator module.
        /// </summary>
        public static ModuleMediatorGuide AddMediator(Action<ModuleMediatorOption>? action = null)
        {
            return new ModuleMediatorGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.Mediator)]
public class ModuleMediator(ModuleMediatorOption option)
    : MoModule<ModuleMediator, ModuleMediatorOption, ModuleMediatorGuide>(option), IWantIterateBusinessTypes
{
    private IServiceCollection? _services;

    public override void ConfigureServices(IServiceCollection services)
    {
        _services = services;
        services.TryAddTransient<IMediator, Mediator>();
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
            RegisterPipelineBehaviors(_services, type);
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

    private static void RegisterPipelineBehaviors(IServiceCollection services, Type implementationType)
    {
        if (implementationType is not { IsClass: true, IsAbstract: false })
        {
            return;
        }

        foreach (var serviceType in implementationType
                     .GetInterfaces()
                     .Where(static serviceType => serviceType.IsGenericType &&
                                                  serviceType.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>)))
        {
            var registrationServiceType = implementationType.IsGenericTypeDefinition
                ? serviceType.GetGenericTypeDefinition()
                : serviceType;

            services.TryAddEnumerable(ServiceDescriptor.Transient(registrationServiceType, implementationType));
        }
    }
}

public class ModuleMediatorGuide : MoModuleGuide<ModuleMediator, ModuleMediatorOption, ModuleMediatorGuide>
{

}

public class ModuleMediatorOption : MoModuleOption<ModuleMediator>
{
}
