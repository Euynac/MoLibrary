using Microsoft.Extensions.DependencyInjection;
using Monica.Experimental.Decorator.Strategies;

namespace Monica.Experimental.Decorator;

public static partial class DecorationExtensions
{
    /// <summary>
    /// Decorates all registered services that implement the specified interface
    /// using the specified decorator type.
    /// </summary>
    /// <typeparam name="TInterface">The interface type that services must implement to be decorated.</typeparam>
    /// <typeparam name="TDecorator">The decorator type that implements the interface.</typeparam>
    /// <param name="services">The services to add to.</param>
    /// <exception cref="DecorationException">If no service implementing <typeparamref name="TInterface"/> has been registered.</exception>
    /// <exception cref="ArgumentNullException">If the <paramref name="services"/> argument is <c>null</c>.</exception>
    public static IServiceCollection DecorateInterfaceProxy<TInterface, TDecorator>(this IServiceCollection services)
        where TDecorator : TInterface
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.DecorateInterfaceProxy(typeof(TInterface), typeof(TDecorator));
    }

    /// <summary>
    /// Decorates all registered services that implement the specified interface
    /// using the specified decorator type.
    /// </summary>
    /// <typeparam name="TInterface">The interface type that services must implement to be decorated.</typeparam>
    /// <typeparam name="TDecorator">The decorator type that implements the interface.</typeparam>
    /// <param name="services">The services to add to.</param>
    /// <exception cref="ArgumentNullException">If the <paramref name="services"/> argument is <c>null</c>.</exception>
    public static bool TryDecorateInterfaceProxy<TInterface, TDecorator>(this IServiceCollection services)
        where TDecorator : TInterface
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.TryDecorateInterfaceProxy(typeof(TInterface), typeof(TDecorator));
    }

    /// <summary>
    /// Decorates all registered services that implement the specified interface
    /// using the specified decorator type.
    /// </summary>
    /// <param name="services">The services to add to.</param>
    /// <param name="interfaceType">The interface type that services must implement to be decorated.</param>
    /// <param name="decoratorType">The decorator type that implements the interface.</param>
    /// <exception cref="DecorationException">If no service implementing the specified <paramref name="interfaceType"/> has been registered.</exception>
    /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>,
    /// <paramref name="interfaceType"/> or <paramref name="decoratorType"/> arguments are <c>null</c>.</exception>
    public static IServiceCollection DecorateInterfaceProxy(this IServiceCollection services, Type interfaceType, Type decoratorType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(interfaceType);
        ArgumentNullException.ThrowIfNull(decoratorType);

        return services.Decorate(DecorationStrategy.WithInterfaceProxy(interfaceType, serviceKey: null, decoratorType));
    }

    /// <summary>
    /// Decorates all registered services that implement the specified interface
    /// using the specified decorator type.
    /// </summary>
    /// <param name="services">The services to add to.</param>
    /// <param name="interfaceType">The interface type that services must implement to be decorated.</param>
    /// <param name="decoratorType">The decorator type that implements the interface.</param>
    /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>,
    /// <paramref name="interfaceType"/> or <paramref name="decoratorType"/> arguments are <c>null</c>.</exception>
    public static bool TryDecorateInterfaceProxy(this IServiceCollection services, Type interfaceType, Type decoratorType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(interfaceType);
        ArgumentNullException.ThrowIfNull(decoratorType);

        return services.TryDecorate(DecorationStrategy.WithInterfaceProxy(interfaceType, serviceKey: null, decoratorType));
    }

    /// <summary>
    /// Decorates all registered services that implement the specified interface
    /// using the <paramref name="decorator"/> function.
    /// </summary>
    /// <typeparam name="TInterface">The interface type that services must implement to be decorated.</typeparam>
    /// <param name="services">The services to add to.</param>
    /// <param name="decorator">The decorator function.</param>
    /// <exception cref="DecorationException">If no service implementing <typeparamref name="TInterface"/> has been registered.</exception>
    /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>
    /// or <paramref name="decorator"/> arguments are <c>null</c>.</exception>
    public static IServiceCollection DecorateInterfaceProxy<TInterface>(this IServiceCollection services, Func<TInterface, TInterface> decorator) where TInterface : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decorator);

        return services.DecorateInterfaceProxy<TInterface>((service, _) => decorator(service));
    }

    /// <summary>
    /// Decorates all registered services that implement the specified interface
    /// using the <paramref name="decorator"/> function.
    /// </summary>
    /// <typeparam name="TInterface">The interface type that services must implement to be decorated.</typeparam>
    /// <param name="services">The services to add to.</param>
    /// <param name="decorator">The decorator function.</param>
    /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>
    /// or <paramref name="decorator"/> arguments are <c>null</c>.</exception>
    public static bool TryDecorateInterfaceProxy<TInterface>(this IServiceCollection services, Func<TInterface, TInterface> decorator) where TInterface : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decorator);

        return services.TryDecorateInterfaceProxy<TInterface>((service, _) => decorator(service));
    }

    /// <summary>
    /// Decorates all registered services that implement the specified interface
    /// using the <paramref name="decorator"/> function.
    /// </summary>
    /// <typeparam name="TInterface">The interface type that services must implement to be decorated.</typeparam>
    /// <param name="services">The services to add to.</param>
    /// <param name="decorator">The decorator function.</param>
    /// <exception cref="DecorationException">If no service implementing <typeparamref name="TInterface"/> has been registered.</exception>
    /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>
    /// or <paramref name="decorator"/> arguments are <c>null</c>.</exception>
    public static IServiceCollection DecorateInterfaceProxy<TInterface>(this IServiceCollection services, Func<TInterface, IServiceProvider, TInterface> decorator) where TInterface : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decorator);

        return services.DecorateInterfaceProxy(typeof(TInterface), (service, provider) => decorator((TInterface) service, provider));
    }

    /// <summary>
    /// Decorates all registered services that implement the specified interface
    /// using the <paramref name="decorator"/> function.
    /// </summary>
    /// <typeparam name="TInterface">The interface type that services must implement to be decorated.</typeparam>
    /// <param name="services">The services to add to.</param>
    /// <param name="decorator">The decorator function.</param>
    /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>
    /// or <paramref name="decorator"/> arguments are <c>null</c>.</exception>
    public static bool TryDecorateInterfaceProxy<TInterface>(this IServiceCollection services, Func<TInterface, IServiceProvider, TInterface> decorator) where TInterface : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decorator);

        return services.TryDecorateInterfaceProxy(typeof(TInterface), (service, provider) => decorator((TInterface) service, provider));
    }

    /// <summary>
    /// Decorates all registered services that implement the specified interface
    /// using the <paramref name="decorator"/> function.
    /// </summary>
    /// <param name="services">The services to add to.</param>
    /// <param name="interfaceType">The interface type that services must implement to be decorated.</param>
    /// <param name="decorator">The decorator function.</param>
    /// <exception cref="DecorationException">If no service implementing the specified <paramref name="interfaceType"/> has been registered.</exception>
    /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>,
    /// <paramref name="interfaceType"/> or <paramref name="decorator"/> arguments are <c>null</c>.</exception>
    public static IServiceCollection DecorateInterfaceProxy(this IServiceCollection services, Type interfaceType, Func<object, IServiceProvider, object> decorator)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(interfaceType);
        ArgumentNullException.ThrowIfNull(decorator);

        return services.Decorate(DecorationStrategy.WithInterfaceProxy(interfaceType, serviceKey: null, decorator));
    }

    /// <summary>
    /// Decorates all registered services that implement the specified interface
    /// using the <paramref name="decorator"/> function.
    /// </summary>
    /// <param name="services">The services to add to.</param>
    /// <param name="interfaceType">The interface type that services must implement to be decorated.</param>
    /// <param name="decorator">The decorator function.</param>
    /// <exception cref="ArgumentNullException">If either the <paramref name="services"/>,
    /// <paramref name="interfaceType"/> or <paramref name="decorator"/> arguments are <c>null</c>.</exception>
    public static bool TryDecorateInterfaceProxy(this IServiceCollection services, Type interfaceType, Func<object, IServiceProvider, object> decorator)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(interfaceType);
        ArgumentNullException.ThrowIfNull(decorator);

        return services.TryDecorate(DecorationStrategy.WithInterfaceProxy(interfaceType, serviceKey: null, decorator));
    }

}
