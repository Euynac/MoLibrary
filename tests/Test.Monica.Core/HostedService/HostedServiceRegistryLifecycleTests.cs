using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Abstractions.Internal;
using Monica.Core.HostedService.Models;
using Monica.Core.HostedService.Services.Support;
using Monica.Core.Modularity.Extensions;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Core.ObservableInstance.Services;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.HostedService;

public sealed class HostedServiceRegistryLifecycleTests
{
    [Fact]
    public async Task StartAsync_WithConcurrentServices_ShouldPublishDistinctInstancesBeforeServicesStart()
    {
        var observer = new RecordingObserver();
        var builder = CreateBuilder();
        builder.Services.AddSingleton<IHostedServiceRuntimeObserver>(observer);
        builder.Services.AddSingleton<IHostedService>(provider =>
            new TestHostedService(provider.GetRequiredService<IMoHostedServiceRegistry>(), "shared"));
        builder.Services.AddSingleton<IHostedService>(provider =>
            new TestHostedService(provider.GetRequiredService<IMoHostedServiceRegistry>(), "shared"));

        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        var registry = host.Services.GetRequiredService<IMoHostedServiceRegistry>();
        var services = registry.GetServices<TestHostedService>();
        services.Should().HaveCount(2);
        services.Select(static service => service.InstanceId).Should().OnlyHaveUniqueItems();
        services.Should().OnlyContain(static service => service.ServiceKey == "shared");
        registry.GetServicesByKey("shared").Should().HaveCount(2);
        host.Services.GetServices<IHostedService>()
            .OfType<TestHostedService>()
            .Should().OnlyContain(static service => service.WasVisibleWhenStarted);

        await host.StopAsync(TestContext.Current.CancellationToken);

        registry.GetServices<TestHostedService>()
            .Should().OnlyContain(static service => service.CurrentState == HostedServiceState.Stopped);
        observer.DisposeCount.Should().Be(2);
        var transitionsAfterStop = observer.TransitionCount;

        foreach (var service in host.Services.GetServices<IHostedService>().OfType<TestHostedService>())
        {
            service.RecordTestTransition();
        }

        observer.TransitionCount.Should().Be(transitionsAfterStop);
    }

    [Fact]
    public async Task StartAsync_WhenTheSameInstanceIsRegisteredTwice_ShouldRejectBeforeServiceStart()
    {
        var builder = CreateBuilder();
        var service = new TestHostedService(null, "duplicate");
        builder.Services.AddSingleton<IHostedService>(service);
        builder.Services.AddSingleton<IHostedService>(service);

        using var host = builder.Build();
        var act = () => host.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*registered more than once*");
        service.StartCount.Should().Be(0);
        host.Services.GetRequiredService<IMoHostedServiceRegistry>()
            .GetAllServices()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task StartingAsync_WhenObserverAttachmentFails_ShouldRollbackObserversAndRegistryPublication()
    {
        var observer = new RecordingObserver(throwOnObservation: 2);
        var builder = CreateBuilder();
        var observableRegistry = new ObservableInstanceRegistry(
            Options.Create(new ModuleObservableInstanceOption()));
        builder.Services.AddSingleton<IObservableInstanceRegistry>(observableRegistry);
        builder.Services.AddSingleton<IHostedServiceRuntimeObserver>(observer);
        builder.Services.AddSingleton<IHostedService>(provider =>
            new RollbackHostedService(
                observableRegistry,
                provider.GetRequiredService<IServiceScopeFactory>(),
                "one"));
        builder.Services.AddSingleton<IHostedService>(provider =>
            new RollbackHostedService(
                observableRegistry,
                provider.GetRequiredService<IServiceScopeFactory>(),
                "two"));

        using var host = builder.Build();
        var act = () => host.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Observer attachment failed.*");
        observer.ObserveCount.Should().Be(2);
        observer.DisposeCount.Should().Be(1);
        host.Services.GetRequiredService<IMoHostedServiceRegistry>()
            .GetAllServices()
            .Should().BeEmpty();
        observableRegistry.GetAllInstances().Should().BeEmpty();
        host.Services.GetServices<IHostedService>()
            .OfType<RollbackHostedService>()
            .Should().OnlyContain(static service => service.StartCount == 0);
    }

    [Fact]
    public async Task Dispose_AfterFailedStartup_ShouldRetryObserverDetachmentsThatPreviouslyFailed()
    {
        var observer = new RetryableRollbackObserver();
        var builder = CreateBuilder();
        builder.Services.AddSingleton<IHostedServiceRuntimeObserver>(observer);
        builder.Services.AddSingleton<IHostedService>(_ => new TestHostedService(null, "one"));
        builder.Services.AddSingleton<IHostedService>(_ => new TestHostedService(null, "two"));

        var host = builder.Build();
        var act = () => host.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<AggregateException>()
            .WithMessage("*observer rollback also reported errors*");
        observer.DisposeAttempts.Should().Be(1);

        host.Dispose();

        observer.DisposeAttempts.Should().Be(2);
        observer.IsDetached.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WithTransientHostedService_ShouldValidateBeforeLifecycleEvenWhenStartupIsConcurrent()
    {
        var observer = new RecordingObserver();
        var builder = CreateBuilder();
        builder.Services.AddSingleton<IHostedServiceRuntimeObserver>(observer);
        builder.Services.AddTransient<IHostedService>(provider =>
            new TestHostedService(provider.GetRequiredService<IMoHostedServiceRegistry>(), "transient"));

        using var host = builder.Build();
        var act = () => host.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OptionsValidationException>()
            .WithMessage("*Every IHostedService registration must be singleton*");
        observer.ObserveCount.Should().Be(0);
        host.Services.GetRequiredService<IMoHostedServiceRegistry>()
            .GetAllServices()
            .Should().BeEmpty();
    }

    private static HostApplicationBuilder CreateBuilder()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.Configure<HostOptions>(options =>
        {
            options.ServicesStartConcurrently = true;
            options.ServicesStopConcurrently = true;
        });
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddHostedService();
        });
        return builder;
    }

    private sealed class TestHostedService : IHostedService, IMoHostedService
    {
        private readonly IMoHostedServiceRegistry? _registry;

        public TestHostedService(IMoHostedServiceRegistry? registry, string? serviceKey)
        {
            _registry = registry;
            ServiceKey = serviceKey;
            var observableRegistry = new ObservableInstanceRegistry(
                Options.Create(new ModuleObservableInstanceOption()));
            var tracker = observableRegistry.Register($"test-hosted-service-{Guid.NewGuid():N}", registration =>
            {
                registration.InstanceName = ServiceName;
                registration.InstanceType = GetType();
                registration.InstanceKey = serviceKey;
            });
            RuntimeInfo = new HostedServiceRuntimeInfo(tracker);
        }

        public string ServiceName => nameof(TestHostedService);

        public string? ServiceKey { get; }

        public string? ServiceGroupId => "Tests";

        public int MaxHistorySize => 100;

        public TimeSpan? HeartbeatInterval => null;

        public HostedServiceRuntimeInfo RuntimeInfo { get; }

        public int StartCount { get; private set; }

        public bool WasVisibleWhenStarted { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCount++;
            WasVisibleWhenStarted = _registry?.GetServiceByInstanceId(RuntimeInfo.InstanceId) is not null;
            RuntimeInfo.Tracker.RecordState("Started", HostedServiceState.Running);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            RuntimeInfo.Tracker.RecordState("Stopped", HostedServiceState.Stopped);
            return Task.CompletedTask;
        }

        public void RecordTestTransition()
        {
            RuntimeInfo.Tracker.RecordState("After observer disposal", HostedServiceState.Degraded);
        }
    }

    private sealed class RollbackHostedService(
        IObservableInstanceRegistry observableRegistry,
        IServiceScopeFactory serviceScopeFactory,
        string serviceKey)
        : MoHostedService(
            observableRegistry,
            Options.Create(new ModuleHostedServiceOption()),
            serviceScopeFactory,
            NullLogger<RollbackHostedService>.Instance)
    {
        public override string? ServiceKey { get; } = serviceKey;

        public int StartCount { get; private set; }

        protected override Task OnStartingAsync(CancellationToken cancellationToken)
        {
            StartCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingObserver(int? throwOnObservation = null) : IHostedServiceRuntimeObserver
    {
        private int _disposeCount;
        private int _transitionCount;

        public int ObserveCount { get; private set; }

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public int TransitionCount => Volatile.Read(ref _transitionCount);

        public IDisposable Observe(IMoHostedService service)
        {
            ObserveCount++;
            if (ObserveCount == throwOnObservation)
            {
                throw new InvalidOperationException("Observer attachment failed.");
            }

            global::Monica.Core.ObservableInstance.Models.ObservableInstanceTracker.StateChangedHandler handler =
                _ => Interlocked.Increment(ref _transitionCount);
            service.RuntimeInfo.Tracker.StateChanged += handler;
            return new Subscription(() =>
            {
                service.RuntimeInfo.Tracker.StateChanged -= handler;
                Interlocked.Increment(ref _disposeCount);
            });
        }
    }

    private sealed class RetryableRollbackObserver : IHostedServiceRuntimeObserver
    {
        private readonly RetryableSubscription _subscription = new();
        private int _observeCount;

        public int DisposeAttempts => _subscription.DisposeAttempts;

        public bool IsDetached => _subscription.IsDetached;

        public IDisposable Observe(IMoHostedService service)
        {
            if (Interlocked.Increment(ref _observeCount) == 2)
            {
                throw new InvalidOperationException("Observer attachment failed.");
            }

            return _subscription;
        }
    }

    private sealed class RetryableSubscription : IDisposable
    {
        public int DisposeAttempts { get; private set; }

        public bool IsDetached { get; private set; }

        public void Dispose()
        {
            DisposeAttempts++;
            if (DisposeAttempts == 1)
            {
                throw new InvalidOperationException("Observer detachment failed.");
            }

            IsDetached = true;
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                dispose();
            }
        }
    }
}
