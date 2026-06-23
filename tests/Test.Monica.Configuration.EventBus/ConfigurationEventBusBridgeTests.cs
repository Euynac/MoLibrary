using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EventBus.Modules;
using Monica.Configuration.EventBus.Services;
using Monica.Configuration.Models;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Models;
using Xunit;

namespace Test.Monica.Configuration.EventBus;

public sealed class ConfigurationEventBusBridgeTests
{
    [Fact]
    public async Task NotifyAsync_ShouldPublishConfigurationNotificationToConfiguredTopic()
    {
        var eventBus = new RecordingDistributedEventBus();
        var services = new ServiceCollection()
            .AddSingleton<IDistributedEventBus>(eventBus)
            .BuildServiceProvider();
        var notifier = new ConfigurationEventBusChangeNotifier(
            services,
            Options.Create(new ModuleConfigurationEventBusOption
            {
                TopicName = "custom.configuration.reload"
            }));
        var notification = CreateNotification();

        await notifier.NotifyAsync(notification, TestContext.Current.CancellationToken);

        eventBus.PublishedEvents.Should().ContainSingle();
        eventBus.PublishedEvents[0].TopicName.Should().Be("custom.configuration.reload");
        eventBus.PublishedEvents[0].EventData.Should().Be(notification);
    }

    [Fact]
    public async Task NotifyAsync_WhenServiceKeyIsConfigured_ShouldUseKeyedDistributedEventBus()
    {
        var defaultEventBus = new RecordingDistributedEventBus();
        var keyedEventBus = new RecordingDistributedEventBus();
        var services = new ServiceCollection()
            .AddSingleton<IDistributedEventBus>(defaultEventBus)
            .AddKeyedSingleton<IDistributedEventBus>("configuration-reload", keyedEventBus)
            .BuildServiceProvider();
        var notifier = new ConfigurationEventBusChangeNotifier(
            services,
            Options.Create(new ModuleConfigurationEventBusOption
            {
                DistributedEventBusServiceKey = "configuration-reload",
                TopicName = "custom.configuration.reload"
            }));
        var notification = CreateNotification();

        await notifier.NotifyAsync(notification, TestContext.Current.CancellationToken);

        defaultEventBus.PublishedEvents.Should().BeEmpty();
        keyedEventBus.PublishedEvents.Should().ContainSingle();
        keyedEventBus.PublishedEvents[0].TopicName.Should().Be("custom.configuration.reload");
        keyedEventBus.PublishedEvents[0].EventData.Should().Be(notification);
    }

    [Fact]
    public async Task StartAsync_ShouldSubscribeAndForwardNotificationsToReceiver()
    {
        var eventBus = new RecordingDistributedEventBus();
        var receiver = new RecordingReloadSignalReceiver();
        var services = new ServiceCollection()
            .AddSingleton<IDistributedEventBus>(eventBus)
            .BuildServiceProvider();
        var hostedService = new ConfigurationEventBusSubscriptionHostedService(
            services,
            Options.Create(new ModuleConfigurationEventBusOption
            {
                TopicName = "custom.configuration.reload"
            }),
            receiver);
        var notification = CreateNotification();

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        await eventBus.PublishAsync(notification, "custom.configuration.reload", TestContext.Current.CancellationToken);

        receiver.ReceivedNotifications.Should().ContainSingle().Which.Should().Be(notification);
    }

    [Fact]
    public async Task StartAsync_WhenServiceKeyIsConfigured_ShouldSubscribeToKeyedDistributedEventBus()
    {
        var defaultEventBus = new RecordingDistributedEventBus();
        var keyedEventBus = new RecordingDistributedEventBus();
        var receiver = new RecordingReloadSignalReceiver();
        var services = new ServiceCollection()
            .AddSingleton<IDistributedEventBus>(defaultEventBus)
            .AddKeyedSingleton<IDistributedEventBus>("configuration-reload", keyedEventBus)
            .BuildServiceProvider();
        var hostedService = new ConfigurationEventBusSubscriptionHostedService(
            services,
            Options.Create(new ModuleConfigurationEventBusOption
            {
                DistributedEventBusServiceKey = "configuration-reload",
                TopicName = "custom.configuration.reload"
            }),
            receiver);
        var notification = CreateNotification();

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        await defaultEventBus.PublishAsync(notification, "custom.configuration.reload", TestContext.Current.CancellationToken);
        await keyedEventBus.PublishAsync(notification, "custom.configuration.reload", TestContext.Current.CancellationToken);

        receiver.ReceivedNotifications.Should().ContainSingle().Which.Should().Be(notification);
    }

    [Fact]
    public async Task StartAsync_WhenDistributedEventBusIsMissing_ShouldFailFast()
    {
        var hostedService = new ConfigurationEventBusSubscriptionHostedService(
            new ServiceCollection().BuildServiceProvider(),
            Options.Create(new ModuleConfigurationEventBusOption()),
            new RecordingReloadSignalReceiver());

        var act = () => hostedService.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*IDistributedEventBus*");
    }

    private static ConfigurationChangeNotification CreateNotification()
    {
        return new ConfigurationChangeNotification
        {
            NotificationId = Guid.NewGuid().ToString("N"),
            OriginInstanceId = "remote",
            StoreKey = "file:default",
            Scope = ConfigurationReloadScope.MonicaProjection,
            DefinitionKey = "Demo.Options",
            Version = 3,
            ChangedTime = DateTimeOffset.UtcNow
        };
    }

    private sealed class RecordingReloadSignalReceiver : IConfigurationReloadSignalReceiver
    {
        private readonly List<ConfigurationChangeNotification> _receivedNotifications = [];

        public IReadOnlyList<ConfigurationChangeNotification> ReceivedNotifications => _receivedNotifications;

        public Task ReceiveAsync(ConfigurationChangeNotification notification, CancellationToken cancellationToken)
        {
            _receivedNotifications.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDistributedEventBus : IDistributedEventBus
    {
        private Func<ConfigurationChangeNotification, Task>? _notificationHandler;
        private string? _subscriptionTopic;

        public List<PublishedEvent> PublishedEvents { get; } = [];

        public IEventSubscriptionRegistry Subscriptions => throw new NotSupportedException();

        public Task PublishAsync<TEvent>(TEvent eventData, string? topicName = null, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            PublishedEvents.Add(new PublishedEvent(typeof(TEvent), eventData, topicName));
            if (eventData is ConfigurationChangeNotification notification
                && string.Equals(topicName, _subscriptionTopic, StringComparison.Ordinal)
                && _notificationHandler is not null)
            {
                return _notificationHandler(notification);
            }

            return Task.CompletedTask;
        }

        public Task BulkPublishAsync<TEvent>(IEnumerable<TEvent> eventDataList, string? topicName = null, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            throw new NotSupportedException();
        }

        public Task<IEventSubscription> SubscribeAsync<TEvent, THandler>(string? topicName = null)
            where TEvent : class
            where THandler : IEventHandler
        {
            throw new NotSupportedException();
        }

        public Task<IEventSubscription> SubscribeAsync<TEvent>(Func<TEvent, Task> handler, string? topicName = null)
            where TEvent : class
        {
            if (typeof(TEvent) != typeof(ConfigurationChangeNotification))
            {
                throw new NotSupportedException();
            }

            _subscriptionTopic = topicName;
            _notificationHandler = notification => handler((TEvent)(object)notification);
            return Task.FromResult<IEventSubscription>(new RecordingEventSubscription());
        }

        public Task PublishAsync(Type eventType, object eventData, string? topicName = null, CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add(new PublishedEvent(eventType, eventData, topicName));
            return Task.CompletedTask;
        }

        public Task BulkPublishAsync(Type eventType, IEnumerable<object> eventDataList, string? topicName = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed record PublishedEvent(Type EventType, object EventData, string? TopicName);

    private sealed class RecordingEventSubscription : IEventSubscription
    {
        public EventSubscriptionId Id { get; } = EventSubscriptionId.NewId();

        public string? ServiceKey => null;

        public Type EventType => typeof(ConfigurationChangeNotification);

        public string TopicName => "custom.configuration.reload";

        public Type? HandlerType => null;

        public IEventHandlerFactory HandlerFactory => throw new NotSupportedException();

        public EventSubscriptionScope Scope => EventSubscriptionScope.Distributed;

        public EventSubscriptionState State => EventSubscriptionState.Active;

        public DateTime CreatedAt { get; } = DateTime.UtcNow;

        public DateTime? ActivatedAt => CreatedAt;

        public DateTime? DeactivatedAt => null;

        public bool IsAutoDiscovered => false;

        public IReadOnlyDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();

        public T? GetMetadata<T>(string key)
        {
            return default;
        }

        public Task ActivateAsync()
        {
            return Task.CompletedTask;
        }

        public Task DeactivateAsync()
        {
            return Task.CompletedTask;
        }

        public Task ReactivateAsync()
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
