using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core.Execution;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.ObservableInstance.Services;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.HostedService;

public sealed class HostedServiceLifecyclePipelineTests
{
    [Fact]
    public async Task MoBackgroundService_ShouldKeepAllLifecycleHooksAndCleanupInsidePipeline()
    {
        var steps = new ConcurrentQueue<string>();
        await using var provider = CreateProvider(steps);
        using var service = new TestBackgroundService(
            steps,
            new ObservableInstanceRegistry(Options.Create(new ModuleObservableInstanceOption())),
            Options.Create(new ModuleHostedServiceOption()),
            provider.GetRequiredService<IServiceScopeFactory>());

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        steps.Should().Equal(
            "Start:pipeline-before",
            "start:starting",
            "background:running",
            "start:started",
            "Start:pipeline-after",
            "Stop:pipeline-before",
            "stop:stopping",
            "background:stopped",
            "stop:stopped",
            "Stop:pipeline-after");
    }

    [Fact]
    public async Task MoHostedService_ShouldKeepAllLifecycleHooksInsidePipeline()
    {
        var steps = new ConcurrentQueue<string>();
        await using var provider = CreateProvider(steps);
        var service = new TestHostedService(
            steps,
            new ObservableInstanceRegistry(Options.Create(new ModuleObservableInstanceOption())),
            Options.Create(new ModuleHostedServiceOption()),
            provider.GetRequiredService<IServiceScopeFactory>());

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        steps.Should().Equal(
            "Start:pipeline-before",
            "start:starting",
            "start:started",
            "Start:pipeline-after",
            "Stop:pipeline-before",
            "stop:stopping",
            "stop:stopped",
            "Stop:pipeline-after");
    }

    [Fact]
    public void LifecycleEntryMethods_ShouldNotBeOverridable()
    {
        var backgroundStart = typeof(MoBackgroundService).GetMethod(nameof(MoBackgroundService.StartAsync));
        var backgroundStop = typeof(MoBackgroundService).GetMethod(nameof(MoBackgroundService.StopAsync));
        var hostedStart = typeof(MoHostedService).GetMethod(nameof(MoHostedService.StartAsync));
        var hostedStop = typeof(MoHostedService).GetMethod(nameof(MoHostedService.StopAsync));

        backgroundStart.Should().NotBeNull();
        backgroundStart!.IsFinal.Should().BeTrue();
        backgroundStop.Should().NotBeNull();
        backgroundStop!.IsFinal.Should().BeTrue();
        hostedStart.Should().NotBeNull();
        hostedStart!.IsFinal.Should().BeTrue();
        hostedStop.Should().NotBeNull();
        hostedStop!.IsFinal.Should().BeTrue();
    }

    private static ServiceProvider CreateProvider(ConcurrentQueue<string> steps)
    {
        return new ServiceCollection()
            .AddSingleton(steps)
            .AddScoped<IExecutionPipeline, LifecycleRecordingPipeline>()
            .BuildServiceProvider();
    }

    private sealed class TestBackgroundService(
        ConcurrentQueue<string> steps,
        ObservableInstanceRegistry observableInstanceRegistry,
        IOptions<ModuleHostedServiceOption> options,
        IServiceScopeFactory serviceScopeFactory)
        : MoBackgroundService(
            observableInstanceRegistry,
            options,
            serviceScopeFactory,
            NullLogger<TestBackgroundService>.Instance)
    {
        private readonly TaskCompletionSource _backgroundStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task OnStartingAsync(CancellationToken cancellationToken)
        {
            steps.Enqueue("start:starting");
            return Task.CompletedTask;
        }

        protected override async Task OnStartedAsync(CancellationToken cancellationToken)
        {
            await _backgroundStarted.Task.WaitAsync(cancellationToken);
            steps.Enqueue("start:started");
        }

        protected override Task OnStoppingAsync(CancellationToken cancellationToken)
        {
            steps.Enqueue("stop:stopping");
            return Task.CompletedTask;
        }

        protected override Task OnStoppedAsync(CancellationToken cancellationToken)
        {
            steps.Enqueue("stop:stopped");
            return Task.CompletedTask;
        }

        protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
        {
            steps.Enqueue("background:running");
            _backgroundStarted.TrySetResult();

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            finally
            {
                steps.Enqueue("background:stopped");
            }
        }
    }

    private sealed class TestHostedService(
        ConcurrentQueue<string> steps,
        ObservableInstanceRegistry observableInstanceRegistry,
        IOptions<ModuleHostedServiceOption> options,
        IServiceScopeFactory serviceScopeFactory)
        : MoHostedService(
            observableInstanceRegistry,
            options,
            serviceScopeFactory,
            NullLogger<TestHostedService>.Instance)
    {
        protected override Task OnStartingAsync(CancellationToken cancellationToken)
        {
            steps.Enqueue("start:starting");
            return Task.CompletedTask;
        }

        protected override Task OnStartedAsync(CancellationToken cancellationToken)
        {
            steps.Enqueue("start:started");
            return Task.CompletedTask;
        }

        protected override Task OnStoppingAsync(CancellationToken cancellationToken)
        {
            steps.Enqueue("stop:stopping");
            return Task.CompletedTask;
        }

        protected override Task OnStoppedAsync(CancellationToken cancellationToken)
        {
            steps.Enqueue("stop:stopped");
            return Task.CompletedTask;
        }
    }

    private sealed class LifecycleRecordingPipeline(ConcurrentQueue<string> steps) : IExecutionPipeline
    {
        public async Task<TResult> ExecuteAsync<TInput, TResult>(
            ExecutionContext<TInput> context,
            ExecutionDelegate<TResult> terminal)
        {
            var feature = context.Features.GetRequired<HostedServiceExecutionFeature>();
            steps.Enqueue($"{feature.Phase}:pipeline-before");

            var result = await terminal();

            steps.Enqueue($"{feature.Phase}:pipeline-after");
            return result;
        }
    }
}
