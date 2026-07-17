using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monica.Authority.Identity.Abstractions;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Services;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Services;
using Monica.EventBus.Services.Support;
using Monica.Repository.Entity.Abstractions;
using Monica.StateStore.Abstractions;
using Monica.Testing.Doubles;

namespace Monica.Testing.Hosting;

/// <summary>
/// Registers deterministic boundary doubles for sociable Monica tests.
/// </summary>
public static class DefaultSeams
{
    /// <summary>
    /// Applies Monica's standard test seams.
    /// </summary>
    public static IServiceCollection AddMonicaTestSeams(this IServiceCollection services)
    {
        services.RemoveAll<ICachedServiceProvider>();
        services.AddScoped<ICachedServiceProvider, CachedServiceProvider>();

        services.RemoveAll<IHttpClientFactory>();
        services.AddSingleton<IHttpClientFactory, TestHttpClientFactory>();

        services.AddXunitTestOutputLogging();

        services.RemoveAll<ICurrentUser>();
        services.AddSingleton<ICurrentUser, TestCurrentUser>();

        services.RemoveAll<IAuditPropertySetter>();
        services.AddSingleton<IAuditPropertySetter, TestAuditPropertySetter>();

        services.RemoveAll<IEventHandlerInvoker>();
        services.AddSingleton<IEventHandlerInvoker, EventHandlerInvoker>();

        services.RemoveAll<IEventSubscriptionRegistry>();
        services.AddSingleton<IEventSubscriptionRegistry, EventSubscriptionRegistry>();

        services.RemoveAll<IDistributedStateStore>();
        services.AddSingleton<IDistributedStateStore, InMemoryDistributedStateStore>();

        services.RemoveAll<RecordingEventBus>();
        services.RemoveAll<ILocalEventBus>();
        services.RemoveAll<IDistributedEventBus>();
        services.AddSingleton<RecordingEventBus>();
        services.AddSingleton<ILocalEventBus>(sp => sp.GetRequiredService<RecordingEventBus>());
        services.AddSingleton<IDistributedEventBus>(sp => sp.GetRequiredService<RecordingEventBus>());

        return services;
    }
}
