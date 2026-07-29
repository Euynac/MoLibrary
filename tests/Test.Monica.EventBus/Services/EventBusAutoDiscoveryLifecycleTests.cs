using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Monica.Core.Modularity.Extensions;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Models;
using Monica.EventBus.Services;
using Monica.EventBus.Services.Support;
using Monica.Modules;
using Xunit;

namespace Test.Monica.EventBus.Services;

public sealed class EventBusAutoDiscoveryLifecycleTests
{
    [Fact]
    public async Task HostLifecycle_ShouldActivateBeforeProviderAndRemoveOnlyOwnedSubscriptionsBeforeProviderStops()
    {
        var builder = CreateBuilder();
        builder.Services.AddSingleton<SubscriptionProviderProbe>();
        builder.Services.AddHostedService(
            serviceProvider => serviceProvider.GetRequiredService<SubscriptionProviderProbe>());

        using var host = builder.Build();
        var registry = host.Services.GetRequiredService<IEventSubscriptionRegistry>();
        var changes = new SubscriptionChangeRecorder();
        using var observation = registry.Subscribe(changes);
        var manual = await AddManualSubscriptionAsync(host, registry);

        await host.StartAsync(TestContext.Current.CancellationToken);
        var ownedCreationOrder = changes.Changes
            .Where(change => change is { ChangeType: EventSubscriptionChangeType.Added, Subscription.IsAutoDiscovered: true })
            .Select(change => change.Subscription.Id)
            .ToArray();
        ownedCreationOrder.Should().HaveCount(2);
        registry.GetAll().Should().HaveCount(ownedCreationOrder.Length + 1);

        await host.StopAsync(TestContext.Current.CancellationToken);

        var probe = host.Services.GetRequiredService<SubscriptionProviderProbe>();
        probe.SubscriptionCountAtStart.Should().Be(ownedCreationOrder.Length + 1);
        probe.SubscriptionCountAtStop.Should().Be(1);
        registry.GetAll().Should().ContainSingle(subscription => subscription.Id == manual.Id);
        changes.Changes
            .Where(change => change is { ChangeType: EventSubscriptionChangeType.Removed, Subscription.IsAutoDiscovered: true })
            .Select(change => change.Subscription.Id)
            .Should().Equal(ownedCreationOrder.Reverse());

        await registry.UnsubscribeAsync(manual.Id, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Dispose_WhenLaterHostedServiceFailsStartup_ShouldRemoveOwnedSubscriptions()
    {
        var builder = CreateBuilder();
        builder.Services.AddHostedService<FailingStartService>();
        var host = builder.Build();
        var registry = host.Services.GetRequiredService<IEventSubscriptionRegistry>();
        var manual = await AddManualSubscriptionAsync(host, registry);

        Func<Task> start = () => host.StartAsync(TestContext.Current.CancellationToken);
        await start.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Provider startup failed.");
        registry.GetAll().Count(subscription => subscription.IsAutoDiscovered).Should().Be(2);

        host.Dispose();

        registry.GetAll().Should().ContainSingle(subscription => subscription.Id == manual.Id);
        await registry.UnsubscribeAsync(manual.Id, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartingAsync_WhenCancelledAfterPartialCreation_ShouldRollBackOwnedSubscriptionsAndPreserveManualSubscription()
    {
        var registry = new ControlledBatchRegistry(blockAfterFirstSubscription: true);
        var builder = CreateBuilder(registry);
        using var host = builder.Build();
        var manual = await AddManualSubscriptionAsync(host, registry);
        using var cancellation = new CancellationTokenSource();

        var startTask = host.StartAsync(cancellation.Token);
        await registry.BatchEntered.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        Func<Task> observeStart = () => startTask;
        await observeStart.Should().ThrowAsync<OperationCanceledException>();
        registry.GetAll().Should().ContainSingle(subscription => subscription.Id == manual.Id);
        registry.RollbackOrder.Should().ContainSingle();

        await registry.UnsubscribeAsync(manual.Id, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartingAndStoppingAsync_WhenConcurrent_ShouldSerializePhasesAndPreserveManualSubscription()
    {
        var registry = new ControlledBatchRegistry(blockAfterFirstSubscription: true);
        var builder = CreateBuilder(registry);
        using var host = builder.Build();
        var manual = await AddManualSubscriptionAsync(host, registry);
        var lifecycle = host.Services.GetServices<IHostedService>()
            .OfType<IHostedLifecycleService>()
            .Single(service => service.GetType().Name == "EventBusAutoDiscoveryLifecycle");

        var startingTask = lifecycle.StartingAsync(TestContext.Current.CancellationToken);
        await registry.BatchEntered.WaitAsync(TestContext.Current.CancellationToken);

        var stoppingTask = lifecycle.StoppingAsync(TestContext.Current.CancellationToken);
        stoppingTask.IsCompleted.Should().BeFalse();

        registry.ReleaseBatch();
        await startingTask;
        await stoppingTask;

        registry.GetAll().Should().ContainSingle(subscription => subscription.Id == manual.Id);
        registry.UnsubscribeOrder
            .Where(subscriptionId => subscriptionId != manual.Id)
            .Should().Equal(registry.OwnedCreationOrder.Reverse());

        await registry.UnsubscribeAsync(manual.Id, TestContext.Current.CancellationToken);
    }

    private static HostApplicationBuilder CreateBuilder(IEventSubscriptionRegistry? registry = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.Configure<HostOptions>(options =>
        {
            options.ServicesStartConcurrently = true;
            options.ServicesStopConcurrently = true;
        });
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options
                .ExcludeDefault()
                .Add(typeof(EventBusAutoDiscoveryLifecycleTests).Assembly));
            monica.AddEventBus();
        });

        if (registry is not null)
        {
            builder.Services.Replace(ServiceDescriptor.Singleton(registry));
        }

        return builder;
    }

    private static Task<IEventSubscription> AddManualSubscriptionAsync(
        IHost host,
        IEventSubscriptionRegistry registry)
    {
        return registry.SubscribeAsync(
            new EventSubscriptionDescriptor
            {
                EventType = typeof(LifecycleEvent),
                TopicName = "manual",
                HandlerFactory = new IocEventHandlerFactory(
                    host.Services.GetRequiredService<IServiceScopeFactory>(),
                    typeof(AutoDiscoveredHandler)),
                Scope = EventSubscriptionScope.Local,
                IsAutoDiscovered = false
            },
            TestContext.Current.CancellationToken);
    }
}

public sealed record LifecycleEvent;

public sealed record SecondaryLifecycleEvent;

public sealed class AutoDiscoveredHandler :
    ILocalEventHandler<LifecycleEvent>,
    ILocalEventHandler<SecondaryLifecycleEvent>
{
    public Task HandleEventAsync(LifecycleEvent eventData, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task HandleEventAsync(SecondaryLifecycleEvent eventData, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class SubscriptionProviderProbe(IEventSubscriptionRegistry registry) : IHostedService
{
    public int SubscriptionCountAtStart { get; private set; }

    public int SubscriptionCountAtStop { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        SubscriptionCountAtStart = registry.GetAll().Count();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        SubscriptionCountAtStop = registry.GetAll().Count();
        return Task.CompletedTask;
    }
}

public sealed class FailingStartService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => Task.FromException(new InvalidOperationException("Provider startup failed."));

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class SubscriptionChangeRecorder : IObserver<EventSubscriptionChange>
{
    private readonly Lock _gate = new();
    private readonly List<EventSubscriptionChange> _changes = [];

    public IReadOnlyList<EventSubscriptionChange> Changes
    {
        get
        {
            lock (_gate)
            {
                return _changes.ToArray();
            }
        }
    }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void OnNext(EventSubscriptionChange value)
    {
        lock (_gate)
        {
            _changes.Add(value);
        }
    }
}

internal sealed class ControlledBatchRegistry(bool blockAfterFirstSubscription) : IEventSubscriptionRegistry
{
    private readonly EventSubscriptionRegistry _inner =
        new(NullLogger<EventSubscriptionRegistry>.Instance);
    private readonly TaskCompletionSource _batchEntered =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _batchRelease =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<EventSubscriptionId> _ownedCreationOrder = [];
    private readonly List<EventSubscriptionId> _rollbackOrder = [];
    private readonly List<EventSubscriptionId> _unsubscribeOrder = [];

    public Task BatchEntered => _batchEntered.Task;

    public IReadOnlyList<EventSubscriptionId> OwnedCreationOrder => _ownedCreationOrder;

    public IReadOnlyList<EventSubscriptionId> RollbackOrder => _rollbackOrder;

    public IReadOnlyList<EventSubscriptionId> UnsubscribeOrder => _unsubscribeOrder;

    public void ReleaseBatch() => _batchRelease.TrySetResult();

    public Task<IEventSubscription> SubscribeAsync(
        EventSubscriptionDescriptor descriptor,
        CancellationToken cancellationToken = default)
        => _inner.SubscribeAsync(descriptor, cancellationToken);

    public async Task<IReadOnlyList<IEventSubscription>> SubscribeBatchAsync(
        IEnumerable<EventSubscriptionDescriptor> descriptors,
        CancellationToken cancellationToken = default)
    {
        var created = new List<IEventSubscription>();
        try
        {
            foreach (var descriptor in descriptors)
            {
                var subscription = await _inner.SubscribeAsync(descriptor, cancellationToken);
                created.Add(subscription);
                _ownedCreationOrder.Add(subscription.Id);

                if (created.Count == 1)
                {
                    _batchEntered.TrySetResult();
                    if (blockAfterFirstSubscription)
                    {
                        await _batchRelease.Task.WaitAsync(cancellationToken);
                    }
                }
            }

            return created;
        }
        catch
        {
            foreach (var subscription in created.AsEnumerable().Reverse())
            {
                await _inner.UnsubscribeAsync(subscription.Id, CancellationToken.None);
                _rollbackOrder.Add(subscription.Id);
            }

            throw;
        }
    }

    public async Task UnsubscribeAsync(
        EventSubscriptionId subscriptionId,
        CancellationToken cancellationToken = default)
    {
        await _inner.UnsubscribeAsync(subscriptionId, cancellationToken);
        _unsubscribeOrder.Add(subscriptionId);
    }

    public Task UnsubscribeBatchAsync(
        IEnumerable<EventSubscriptionId> subscriptionIds,
        CancellationToken cancellationToken = default)
        => UnsubscribeSequentiallyAsync(subscriptionIds, cancellationToken);

    public Task UnsubscribeWhereAsync(
        Func<IEventSubscription, bool> predicate,
        CancellationToken cancellationToken = default)
        => UnsubscribeSequentiallyAsync(
            _inner.GetAll().Where(predicate).Select(subscription => subscription.Id),
            cancellationToken);

    public IQueryable<IEventSubscription> GetAll() => _inner.GetAll();

    public IEventSubscription? GetById(EventSubscriptionId subscriptionId) => _inner.GetById(subscriptionId);

    public IReadOnlyList<IEventSubscription> GetByServiceKey(string? serviceKey) => _inner.GetByServiceKey(serviceKey);

    public IReadOnlyList<IEventSubscription> GetByTopicName(string topicName) => _inner.GetByTopicName(topicName);

    public IReadOnlyList<IEventSubscription> GetByEventType(Type eventType) => _inner.GetByEventType(eventType);

    public IReadOnlyList<IEventSubscription> GetByState(EventSubscriptionState state) => _inner.GetByState(state);

    public IReadOnlyList<IEventSubscription> GetByScope(EventSubscriptionScope scope) => _inner.GetByScope(scope);

    public Task ActivateAsync(
        EventSubscriptionId subscriptionId,
        CancellationToken cancellationToken = default)
        => _inner.ActivateAsync(subscriptionId, cancellationToken);

    public Task DeactivateAsync(
        EventSubscriptionId subscriptionId,
        CancellationToken cancellationToken = default)
        => _inner.DeactivateAsync(subscriptionId, cancellationToken);

    public Task ReactivateAsync(
        EventSubscriptionId subscriptionId,
        CancellationToken cancellationToken = default)
        => _inner.ReactivateAsync(subscriptionId, cancellationToken);

    public Task UnsubscribeByEventTypeAsync(
        Type eventType,
        CancellationToken cancellationToken = default)
        => UnsubscribeSequentiallyAsync(
            _inner.GetByEventType(eventType).Select(subscription => subscription.Id),
            cancellationToken);

    public Task UnsubscribeByHandlerTypeAsync(
        Type handlerType,
        CancellationToken cancellationToken = default)
        => UnsubscribeSequentiallyAsync(
            _inner.GetAll()
                .Where(subscription => subscription.HandlerType == handlerType)
                .Select(subscription => subscription.Id),
            cancellationToken);

    public Task UnsubscribeByServiceKeyAsync(
        string? serviceKey,
        CancellationToken cancellationToken = default)
        => UnsubscribeSequentiallyAsync(
            _inner.GetByServiceKey(serviceKey).Select(subscription => subscription.Id),
            cancellationToken);

    public IDisposable Subscribe(IObserver<EventSubscriptionChange> observer) => _inner.Subscribe(observer);

    private async Task UnsubscribeSequentiallyAsync(
        IEnumerable<EventSubscriptionId> subscriptionIds,
        CancellationToken cancellationToken)
    {
        foreach (var subscriptionId in subscriptionIds.ToArray())
        {
            await UnsubscribeAsync(subscriptionId, cancellationToken);
        }
    }
}
