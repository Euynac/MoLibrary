using System.Text;
using System.Text.Json;
using Dapr.Messaging.PublishSubscribe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core.JsonSerialization.Services;
using Monica.Core.ObservableInstance.Services;
using Monica.Dapr.Abstractions;
using Monica.Dapr.Services;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Models;
using Monica.EventBus.Services;
using Monica.EventBus.Services.Support;
using Monica.Modules;
using NSubstitute;
using Xunit;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Test.Monica.Dapr.Services;

public sealed class DaprEventBusSubscriptionHostedServiceTests
{
    [Fact]
    public async Task Subscription_UsesRetryAsTheDefaultTimeoutResponse()
    {
        using var fixture = await CreateFixtureAsync((_, _) => Task.CompletedTask);

        Assert.Equal(
            TopicResponseAction.Retry,
            fixture.Client.Options.MessageHandlingPolicy.DefaultResponseAction);
    }

    [Fact]
    public async Task MessageHandler_WhenDispatchSucceeds_ReturnsSuccess()
    {
        var received = new List<TestEvent>();
        using var fixture = await CreateFixtureAsync((message, _) =>
        {
            received.Add(message);
            return Task.CompletedTask;
        });

        var action = await fixture.HandleAsync(
            "{\"value\":\"ready\"}",
            TestContext.Current.CancellationToken);

        Assert.Equal(TopicResponseAction.Success, action);
        Assert.Collection(received, message => Assert.Equal("ready", message.Value));
    }

    [Fact]
    public async Task MessageHandler_WhenJsonIsMalformed_ReturnsDrop()
    {
        using var fixture = await CreateFixtureAsync((_, _) => Task.CompletedTask);

        var action = await fixture.HandleAsync(
            "{not-json",
            TestContext.Current.CancellationToken);

        Assert.Equal(TopicResponseAction.Drop, action);
    }

    [Fact]
    public async Task MessageHandler_WhenJsonDeserializesToNull_ReturnsDrop()
    {
        using var fixture = await CreateFixtureAsync((_, _) => Task.CompletedTask);

        var action = await fixture.HandleAsync(
            "null",
            TestContext.Current.CancellationToken);

        Assert.Equal(TopicResponseAction.Drop, action);
    }

    [Fact]
    public async Task MessageHandler_WhenEventHandlerFails_ReturnsRetry()
    {
        using var fixture = await CreateFixtureAsync(
            (_, _) => Task.FromException(new InvalidOperationException("SignalR unavailable.")));

        var action = await fixture.HandleAsync(
            "{\"value\":\"retry\"}",
            TestContext.Current.CancellationToken);

        Assert.Equal(TopicResponseAction.Retry, action);
    }

    [Fact]
    public async Task MessageHandler_WhenEventHandlerIsCancelled_ReturnsRetry()
    {
        using var fixture = await CreateFixtureAsync(
            (_, _) => Task.FromCanceled(new CancellationToken(canceled: true)));

        var action = await fixture.HandleAsync(
            "{\"value\":\"retry\"}",
            TestContext.Current.CancellationToken);

        Assert.Equal(TopicResponseAction.Retry, action);
    }

    [Fact]
    public async Task MessageHandler_PropagatesDeadlineCancellationToTheEventHandler()
    {
        var receivedToken = new TaskCompletionSource<CancellationToken>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var fixture = await CreateFixtureAsync((_, cancellationToken) =>
        {
            receivedToken.TrySetResult(cancellationToken);
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });
        using var cancellation = new CancellationTokenSource();

        var handlingTask = fixture.HandleAsync("{\"value\":\"retry\"}", cancellation.Token);
        var handlerToken = await receivedToken.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        var action = await handlingTask.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(cancellation.Token, handlerToken);
        Assert.Equal(TopicResponseAction.Retry, action);
        await fixture.HandlerDisposed.WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MessageHandler_WhenNonCooperativeHandlerExceedsDeadline_ReturnsRetryWithoutDisposingItsScope()
    {
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var fixture = await CreateFixtureAsync((_, _) =>
        {
            handlerStarted.TrySetResult();
            return releaseHandler.Task;
        });
        using var cancellation = new CancellationTokenSource();

        var handlingTask = fixture.HandleAsync("{\"value\":\"retry\"}", cancellation.Token);
        await handlerStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        var action = await handlingTask.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(TopicResponseAction.Retry, action);
        Assert.False(releaseHandler.Task.IsCompleted);
        Assert.False(fixture.HandlerDisposed.IsCompleted);

        releaseHandler.TrySetResult();
        await fixture.HandlerDisposed.WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MessageHandler_ObservesFailureFromHandlerThatFaultsAfterDeadline()
    {
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var fixture = await CreateFixtureAsync((_, _) =>
        {
            handlerStarted.TrySetResult();
            return handlerCompletion.Task;
        });
        using var cancellation = new CancellationTokenSource();

        var handlingTask = fixture.HandleAsync("{\"value\":\"retry\"}", cancellation.Token);
        await handlerStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        var action = await handlingTask.WaitAsync(TestContext.Current.CancellationToken);

        var lateException = new InvalidOperationException("Late SignalR failure.");
        handlerCompletion.SetException(lateException);
        var observedException = await fixture.Logger.LateFailureObserved
            .WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(TopicResponseAction.Retry, action);
        Assert.Same(lateException, observedException);
        await fixture.HandlerDisposed.WaitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<TestFixture> CreateFixtureAsync(
        Func<TestEvent, CancellationToken, Task> handler)
    {
        var registry = new EventSubscriptionRegistry(NullLogger<EventSubscriptionRegistry>.Instance);
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var eventBus = new TestDistributedEventBus(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            new EventHandlerInvoker(),
            registry);
        var client = new CapturingDaprPublishSubscribeClient();
        var logger = new TestLogger<DaprEventBusSubscriptionHostedService>();
        var handlerDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new DaprEventBusSubscriptionHostedService(
            client,
            registry,
            Substitute.For<IHostApplicationLifetime>(),
            eventBus,
            new ObservableInstanceRegistry(Options.Create(new ModuleObservableInstanceOption())),
            Substitute.For<IDaprSidecarHealthCoordinator>(),
            Options.Create(new ModuleDaprEventBusOption()),
            Options.Create(new ModuleHostedServiceOption
            {
                DefaultHeartbeatInterval = TimeSpan.Zero
            }),
            new JsonSerializerOptionsProvider(new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            logger);
        var subscription = await registry.SubscribeAsync(new EventSubscriptionDescriptor
        {
            EventType = typeof(TestEvent),
            TopicName = "test.progress",
            HandlerFactory = new DelegateHandlerFactory(handler, () => handlerDisposed.TrySetResult()),
            Scope = EventSubscriptionScope.Distributed
        });

        service.OnNext(new EventSubscriptionChange(
            EventSubscriptionChangeType.Added,
            subscription,
            DateTime.UtcNow));

        Assert.NotNull(client.Handler);
        Assert.NotNull(client.Options);
        return new TestFixture(serviceProvider, service, client, logger, handlerDisposed.Task);
    }

    private sealed class TestFixture(
        ServiceProvider serviceProvider,
        DaprEventBusSubscriptionHostedService service,
        CapturingDaprPublishSubscribeClient client,
        TestLogger<DaprEventBusSubscriptionHostedService> logger,
        Task handlerDisposed) : IDisposable
    {
        public CapturingDaprPublishSubscribeClient Client { get; } = client;

        public TestLogger<DaprEventBusSubscriptionHostedService> Logger { get; } = logger;

        public Task HandlerDisposed { get; } = handlerDisposed;

        public Task<TopicResponseAction> HandleAsync(
            string json,
            CancellationToken cancellationToken = default)
        {
            var message = new TopicMessage(
                "message-1",
                "test",
                nameof(TestEvent),
                "1.0",
                "application/json",
                "test.progress",
                "pubsub")
            {
                Data = Encoding.UTF8.GetBytes(json)
            };
            return Client.Handler(message, cancellationToken);
        }

        public void Dispose()
        {
            service.Dispose();
            Client.Dispose();
            serviceProvider.Dispose();
        }
    }

    private sealed class CapturingDaprPublishSubscribeClient()
        : DaprPublishSubscribeClient(Substitute.For<P.Dapr.DaprClient>(), new HttpClient())
    {
        public DaprSubscriptionOptions Options { get; private set; } = null!;

        public TopicMessageHandler Handler { get; private set; } = null!;

        public override Task<IAsyncDisposable> SubscribeAsync(
            string pubSubName,
            string topicName,
            DaprSubscriptionOptions options,
            TopicMessageHandler messageHandler,
            CancellationToken cancellationToken = default)
        {
            Options = options;
            Handler = messageHandler;
            return Task.FromResult<IAsyncDisposable>(NoOpAsyncDisposable.Instance);
        }
    }

    private sealed class TestDistributedEventBus(
        IServiceScopeFactory serviceScopeFactory,
        IEventHandlerInvoker eventHandlerInvoker,
        IEventSubscriptionRegistry subscriptionManager)
        : DistributedEventBusBase(
            serviceScopeFactory,
            eventHandlerInvoker,
            subscriptionManager,
            NullLoggerFactory.Instance)
    {
        public override Task PublishAsync(
            Type eventType,
            object eventData,
            string? topicName = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public override Task BulkPublishAsync(
            Type eventType,
            IEnumerable<object> eventDataList,
            string? topicName = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class DelegateHandlerFactory(
        Func<TestEvent, CancellationToken, Task> handler,
        Action onDispose) : IEventHandlerFactory
    {
        public IEventHandlerDisposeWrapper GetHandler()
        {
            return new EventHandlerDisposeWrapper(new DelegateHandler(handler), onDispose);
        }

        public Type GetHandlerType()
        {
            return typeof(DelegateHandler);
        }
    }

    private sealed class DelegateHandler(Func<TestEvent, CancellationToken, Task> handler)
        : IDistributedEventHandler<TestEvent>
    {
        public Task HandleEventAsync(TestEvent eventData, CancellationToken cancellationToken)
        {
            return handler(eventData, cancellationToken);
        }
    }

    private sealed record TestEvent(string Value);

    private sealed class TestLogger<T> : ILogger<T>
    {
        private readonly TaskCompletionSource<Exception> _lateFailureObserved = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Exception> LateFailureObserved => _lateFailureObserved.Task;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (exception is not null
                && message.Contains(
                    "faulted after the callback returned Retry",
                    StringComparison.Ordinal))
            {
                _lateFailureObserved.TrySetResult(exception);
            }
        }
    }

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public static NoOpAsyncDisposable Instance { get; } = new();

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
