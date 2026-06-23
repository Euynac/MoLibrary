using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EventBus.Modules;
using Monica.Configuration.Models;
using Monica.EventBus.Abstractions;

namespace Monica.Configuration.EventBus.Services;

/// <summary>
/// Subscribes to distributed configuration reload signals and forwards them to the local receiver.
/// </summary>
public sealed class ConfigurationEventBusSubscriptionHostedService(
    IServiceProvider serviceProvider,
    IOptions<ModuleConfigurationEventBusOption> options,
    IConfigurationReloadSignalReceiver receiver)
    : IHostedService
{
    private IDistributedEventBus? _eventBus;
    private IEventSubscription? _subscription;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _eventBus = ConfigurationEventBusChangeNotifier.ResolveDistributedEventBus(serviceProvider);
        _subscription = await _eventBus.SubscribeAsync<ConfigurationChangeNotification>(
            notification => receiver.ReceiveAsync(notification, CancellationToken.None),
            options.Value.TopicName);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscription is null)
        {
            return;
        }

        if (_eventBus is not null)
        {
            await _eventBus.Subscriptions.UnsubscribeAsync(_subscription.Id);
        }
        else
        {
            await _subscription.DisposeAsync();
        }

        _subscription = null;
        _eventBus = null;
    }
}
