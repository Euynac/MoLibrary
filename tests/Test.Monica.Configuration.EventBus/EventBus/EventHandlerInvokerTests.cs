using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Monica.Core.Execution;
using Monica.Core.Modularity.Extensions;
using Monica.EventBus;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Models;
using Monica.EventBus.Providers.Local;
using Monica.EventBus.Services;
using Monica.EventBus.Services.Support;
using Monica.Modules;
using NSubstitute;
using Xunit;

namespace Test.Monica.Configuration.EventBus.EventBus;

public sealed class EventHandlerInvokerTests
{
    [Fact]
    public async Task PublishAsync_WhenHandlerScopeContainsAsyncOnlyDisposables_ShouldAwaitDisposalAfterExecution()
    {
        var trace = new List<string>();
        using var host = BuildExecutionHost(trace);
        var eventBus = new LocalEventBus(
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            new EventHandlerInvoker(),
            new EventSubscriptionRegistry(NullLogger<EventSubscriptionRegistry>.Instance),
            NullLoggerFactory.Instance);
        await eventBus.SubscribeAsync<TestEvent, AsyncOnlyDisposableHandler>();

        await eventBus.PublishAsync(
            new TestEvent("value"),
            cancellationToken: TestContext.Current.CancellationToken);

        trace.Should().ContainInOrder(
            "behavior:enter",
            "handler:execute",
            "behavior:exit");
        trace.IndexOf("behavior:disposed").Should().BeGreaterThan(trace.IndexOf("behavior:exit"));
        trace.IndexOf("handler:disposed").Should().BeGreaterThan(trace.IndexOf("handler:execute"));
    }

    [Fact]
    public async Task PublishAsync_WhenHandlerResolutionFails_ShouldDisposeTheIncompleteScopeAsynchronously()
    {
        var trace = new List<string>();
        await using var provider = new ServiceCollection()
            .AddSingleton(trace)
            .AddScoped<AsyncOnlyDisposableHandlerDependency>()
            .AddScoped<ThrowingHandler>()
            .BuildServiceProvider();
        var eventBus = new LocalEventBus(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new EventHandlerInvoker(),
            new EventSubscriptionRegistry(NullLogger<EventSubscriptionRegistry>.Instance),
            NullLoggerFactory.Instance);
        await eventBus.SubscribeAsync<TestEvent, ThrowingHandler>();

        Func<Task> publish = () => eventBus.PublishAsync(
            new TestEvent("value"),
            cancellationToken: TestContext.Current.CancellationToken);

        await publish.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("handler activation failed");
        trace.Should().Equal("dependency:disposed");
    }

    [Theory]
    [InlineData(EventSubscriptionScope.Local)]
    [InlineData(EventSubscriptionScope.Distributed)]
    public async Task InvokeAsync_ShouldDispatchOnlyTheSubscriptionContractFromTheHandlerScope(
        EventSubscriptionScope scope)
    {
        var capture = new ExecutionCapture();
        using var provider = new ServiceCollection()
            .AddSingleton(capture)
            .AddScoped<ScopeIdentity>()
            .AddScoped<IExecutionPipeline, RecordingPipeline>()
            .BuildServiceProvider();
        using var handlerScope = provider.CreateScope();
        var expectedScopeIdentity = handlerScope.ServiceProvider.GetRequiredService<ScopeIdentity>().Value;
        var subscription = Substitute.For<IEventSubscription>();
        subscription.Scope.Returns(scope);
        subscription.TopicName.Returns("test.topic");
        subscription.ServiceKey.Returns("test-bus");
        var handler = new DualContractHandler();

        await new EventHandlerInvoker().InvokeAsync(
            handler,
            new TestEvent("value"),
            typeof(TestEvent),
            subscription,
            handlerScope.ServiceProvider,
            TestContext.Current.CancellationToken);

        handler.LocalInvocations.Should().Be(scope == EventSubscriptionScope.Local ? 1 : 0);
        handler.DistributedInvocations.Should().Be(scope == EventSubscriptionScope.Distributed ? 1 : 0);
        capture.Point.Should().Be(
            scope == EventSubscriptionScope.Local
                ? EventBusExecutionPoints.LocalHandler
                : EventBusExecutionPoints.DistributedHandler);
        capture.ScopeIdentity.Should().Be(expectedScopeIdentity);
        capture.TransactionMode.Should().Be(ExecutionTransactionMode.Automatic);
        capture.Feature.Should().NotBeNull();
        capture.Feature!.TopicName.Should().Be("test.topic");
        capture.Feature.Scope.Should().Be(scope);
    }

    private sealed record TestEvent(string Value);

    private static IHost BuildExecutionHost(List<string> trace)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddSingleton(trace)
            .AddScoped<AsyncOnlyDisposableHandler>();
        builder.AddMonica(monica => monica
            .AddExecutionPipeline()
            .AddBehavior<AsyncOnlyDisposableBehavior>(
                descriptorFilter: static descriptor =>
                    descriptor.Point == EventBusExecutionPoints.LocalHandler,
                lifetime: ServiceLifetime.Scoped));
        return builder.Build();
    }

    private sealed class DualContractHandler :
        ILocalEventHandler<TestEvent>,
        IDistributedEventHandler<TestEvent>
    {
        public int LocalInvocations { get; private set; }

        public int DistributedInvocations { get; private set; }

        Task ILocalEventHandler<TestEvent>.HandleEventAsync(
            TestEvent eventData,
            CancellationToken cancellationToken)
        {
            LocalInvocations++;
            return Task.CompletedTask;
        }

        Task IDistributedEventHandler<TestEvent>.HandleEventAsync(
            TestEvent eventData,
            CancellationToken cancellationToken)
        {
            DistributedInvocations++;
            return Task.CompletedTask;
        }
    }

    private sealed class AsyncOnlyDisposableHandler(List<string> trace) :
        ILocalEventHandler<TestEvent>,
        IAsyncDisposable
    {
        public Task HandleEventAsync(TestEvent eventData, CancellationToken cancellationToken)
        {
            trace.Add("handler:execute");
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            trace.Add("handler:disposed");
        }
    }

    private sealed class ThrowingHandler : ILocalEventHandler<TestEvent>
    {
        public ThrowingHandler(AsyncOnlyDisposableHandlerDependency dependency)
        {
            _ = dependency;
            throw new InvalidOperationException("handler activation failed");
        }

        public Task HandleEventAsync(TestEvent eventData, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class AsyncOnlyDisposableHandlerDependency(List<string> trace) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            trace.Add("dependency:disposed");
        }
    }

    private sealed class AsyncOnlyDisposableBehavior(List<string> trace) :
        IExecutionBehavior<TestEvent, ExecutionUnit>,
        IAsyncDisposable
    {
        public async Task<ExecutionUnit> ExecuteAsync(
            ExecutionContext<TestEvent> context,
            ExecutionDelegate<ExecutionUnit> next)
        {
            trace.Add("behavior:enter");
            var result = await next();
            trace.Add("behavior:exit");
            return result;
        }

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            trace.Add("behavior:disposed");
        }
    }

    private sealed class ScopeIdentity
    {
        public Guid Value { get; } = Guid.NewGuid();
    }

    private sealed class ExecutionCapture
    {
        public ExecutionPoint? Point { get; set; }

        public Guid ScopeIdentity { get; set; }

        public ExecutionTransactionMode TransactionMode { get; set; }

        public EventHandlerExecutionFeature? Feature { get; set; }
    }

    private sealed class RecordingPipeline(
        ScopeIdentity scopeIdentity,
        ExecutionCapture capture) : IExecutionPipeline
    {
        public Task<TResult> ExecuteAsync<TInput, TResult>(
            ExecutionDescriptor descriptor,
            TInput input,
            object? target,
            ExecutionDelegate<TResult> terminal,
            CancellationToken cancellationToken = default,
            ExecutionFeatureCollection? features = null)
        {
            Record(descriptor, features);
            return terminal();
        }

        public Task ExecuteAsync<TInput>(
            ExecutionDescriptor descriptor,
            TInput input,
            object? target,
            Func<Task> terminal,
            CancellationToken cancellationToken = default,
            ExecutionFeatureCollection? features = null)
        {
            Record(descriptor, features);
            return terminal();
        }

        private void Record(ExecutionDescriptor descriptor, ExecutionFeatureCollection? features)
        {
            capture.Point = descriptor.Point;
            capture.ScopeIdentity = scopeIdentity.Value;
            capture.TransactionMode = descriptor.TransactionMode;
            capture.Feature = features?.GetRequired<EventHandlerExecutionFeature>();
        }
    }
}
