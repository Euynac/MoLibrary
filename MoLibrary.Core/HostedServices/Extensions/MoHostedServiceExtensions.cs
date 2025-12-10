using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MoLibrary.Core.HostedServices.Interfaces;
using MoLibrary.Core.HostedServices.Models;

namespace MoLibrary.Core.HostedServices.Extensions;

/// <summary>
/// Extension methods for registering observable MoHostedServices
/// </summary>
public static class MoHostedServiceExtensions
{
    /// <summary>
    /// Adds an observable MoHostedService to the service collection with automatic metadata tracking
    /// </summary>
    /// <typeparam name="THostedService">The hosted service type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="serviceKey">Optional service key for keyed service instances</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMoHostedService<THostedService>(
        this IServiceCollection services,
        string? serviceKey = null)
        where THostedService : class, IHostedService
    {
        // Register the hosted service using standard Microsoft extension
        services.AddHostedService<THostedService>();

        // Register post-configuration to initialize observable info after service creation
        services.AddSingleton<IHostedServiceRegistrationAction>(provider =>
            new HostedServiceRegistrationAction<THostedService>(typeof(THostedService), serviceKey));

        return services;
    }

    /// <summary>
    /// Adds an observable MoHostedService with a factory to the service collection
    /// </summary>
    /// <typeparam name="THostedService">The hosted service type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="implementationFactory">Factory function to create the service instance</param>
    /// <param name="serviceKey">Optional service key for keyed service instances</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMoHostedService<THostedService>(
        this IServiceCollection services,
        Func<IServiceProvider, THostedService> implementationFactory,
        string? serviceKey = null)
        where THostedService : class, IHostedService
    {
        // Register the hosted service with factory
        services.AddHostedService(implementationFactory);

        // Register post-configuration to initialize observable info after service creation
        services.AddSingleton<IHostedServiceRegistrationAction>(provider =>
            new HostedServiceRegistrationAction<THostedService>(typeof(THostedService), serviceKey));

        return services;
    }

    /// <summary>
    /// Internal marker interface for registration actions
    /// </summary>
    internal interface IHostedServiceRegistrationAction
    {
        void Execute(IServiceProvider provider);
        Type ServiceType { get; }
    }

    /// <summary>
    /// Internal registration action that initializes observable info and registers with manager
    /// </summary>
    private class HostedServiceRegistrationAction<THostedService>(Type serviceType, string? serviceKey)
        : IHostedServiceRegistrationAction
        where THostedService : class, IHostedService
    {
        public Type ServiceType => serviceType;

        public void Execute(IServiceProvider provider)
        {
            var serviceName = serviceType.Name;

            // Try to get the actual service instance first
            var hostedServices = provider.GetServices<IHostedService>();
            var instance = hostedServices.OfType<THostedService>().FirstOrDefault();

            if (instance != null)
            {
                // Extract heartbeat interval if this is a MoBackgroundService
                TimeSpan? heartbeatInterval = null;
                if (instance is MoBackgroundService moBackground)
                {
                    var heartbeatProperty = moBackground.GetType()
                        .GetProperty("HeartbeatInterval",
                            System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Public);

                    if (heartbeatProperty != null)
                    {
                        heartbeatInterval = heartbeatProperty.GetValue(moBackground) as TimeSpan?;
                    }
                }

                // Create observable info with all properties including heartbeat interval
                var info = new HostedServiceObservableInfo
                {
                    ServiceName = serviceName,
                    ServiceType = serviceType,
                    ServiceKey = serviceKey,
                    RegisteredAt = DateTime.UtcNow,
                    CurrentState = HostedServiceState.NotStarted,
                    HeartbeatInterval = heartbeatInterval
                };

                // Initialize observable info on the service
                if (instance is MoHostedService moHosted)
                {
                    moHosted.InitializeObservableInfo(info);
                }
                else if (instance is MoBackgroundService moBackgroundService)
                {
                    moBackgroundService.InitializeObservableInfo(info);
                }

                // Register with manager
                var manager = provider.GetRequiredService<IMoHostedServiceManager>();

                // Get exception pool
                ExceptionHandler.ExceptionPool.ExceptionPool? pool = null;
                if (instance is MoHostedService hostedWithPool)
                {
                    pool = hostedWithPool.ExceptionPool;
                }
                else if (instance is MoBackgroundService backgroundWithPool)
                {
                    pool = backgroundWithPool.ExceptionPool;
                }

                (manager as MoHostedServiceManager)!.RegisterService(serviceType, info, pool);
            }
        }
    }
}
