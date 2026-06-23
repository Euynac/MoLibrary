using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EventBus.Modules;
using Monica.Configuration.Models;
using Monica.EventBus.Abstractions;
using Monica.Modules;

namespace Monica.Configuration.EventBus.Services;

/// <summary>
/// Publishes Monica configuration reload notifications to the distributed EventBus.
/// </summary>
public sealed class ConfigurationEventBusChangeNotifier(
    IServiceProvider serviceProvider,
    IOptions<ModuleConfigurationEventBusOption> options)
    : IConfigurationChangeNotifier
{
    /// <inheritdoc />
    public Task NotifyAsync(ConfigurationChangeNotification notification, CancellationToken cancellationToken)
    {
        return ResolveDistributedEventBus(serviceProvider)
            .PublishAsync(notification, options.Value.TopicName, cancellationToken);
    }

    internal static IDistributedEventBus ResolveDistributedEventBus(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<IDistributedEventBus>()
            ?? throw new InvalidOperationException(
                $"{nameof(ModuleConfigurationEventBus)} requires an {nameof(IDistributedEventBus)}. " +
                $"Configure a distributed EventBus provider, for example with {nameof(ModuleEventBusGuide.UseDistributedEventBus)}.");
    }
}
