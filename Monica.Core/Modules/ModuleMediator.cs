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

    internal static void RegisterPipelineBehavior(IServiceCollection services, Type implementationType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(implementationType);

        if (implementationType is not { IsClass: true, IsAbstract: false })
        {
            throw new ArgumentException("Pipeline behavior must be a non-abstract class.", nameof(implementationType));
        }

        var behaviorInterfaces = implementationType
            .GetInterfaces()
            .Where(static serviceType => serviceType.IsGenericType &&
                                         serviceType.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>))
            .ToArray();

        if (behaviorInterfaces.Length == 0)
        {
            throw new ArgumentException(
                $"Type '{implementationType.FullName}' does not implement IPipelineBehavior<,>.",
                nameof(implementationType));
        }

        foreach (var serviceType in behaviorInterfaces)
        {
            var registrationServiceType = implementationType.IsGenericTypeDefinition
                ? serviceType.GetGenericTypeDefinition()
                : serviceType;

            services.TryAddEnumerable(ServiceDescriptor.Transient(registrationServiceType, implementationType));
        }
    }
}

public class ModuleMediatorGuide : ModuleGuide<ModuleMediator, ModuleMediatorOption, ModuleMediatorGuide>
{
    public ModuleMediatorGuide AddPipelineBehavior(Type behaviorType)
    {
        var behaviorKey = behaviorType.AssemblyQualifiedName ?? behaviorType.FullName ?? behaviorType.Name;
        ConfigureServices(
            context => ModuleMediator.RegisterPipelineBehavior(context.Services, behaviorType),
            secondKey: behaviorKey);
        return this;
    }

    public ModuleMediatorGuide AddPipelineBehavior<TBehavior>()
        where TBehavior : class
    {
        return AddPipelineBehavior(typeof(TBehavior));
    }
}

public class ModuleMediatorOption : ModuleOptions<ModuleMediator>
{
}
