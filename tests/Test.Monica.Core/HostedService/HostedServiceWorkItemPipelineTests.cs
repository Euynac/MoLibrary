using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core.Execution;
using Monica.Core.HostedService;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.ObservableInstance.Services;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.HostedService;

public sealed class HostedServiceWorkItemPipelineTests
{
    [Fact]
    public async Task ExecuteWorkItemAsync_ShouldResolveWorkerAndPipelineFromOneDisposedScope()
    {
        var capture = new WorkItemCapture();
        await using var provider = new ServiceCollection()
            .AddSingleton(capture)
            .AddScoped<ScopeIdentity>()
            .AddScoped<ScopedWorkItem>()
            .AddScoped<IExecutionPipeline, RecordingPipeline>()
            .BuildServiceProvider();
        var service = new TestHostedService(
            new ObservableInstanceRegistry(Options.Create(new ModuleObservableInstanceOption())),
            Options.Create(new ModuleHostedServiceOption()),
            provider.GetRequiredService<IServiceScopeFactory>());

        var result = await service.RunWorkItemAsync(
            new WorkItemInput("value"),
            TestContext.Current.CancellationToken);

        result.Should().Be("value");
        capture.PipelineScopeIdentity.Should().Be(capture.WorkItemScopeIdentity);
        capture.Point.Should().Be(HostedServiceExecutionPoints.WorkItem);
        capture.Feature.Should().NotBeNull();
        capture.Feature!.ServiceName.Should().Be(nameof(TestHostedService));
        capture.WorkItemDisposed.Should().BeTrue();
    }

    private sealed class TestHostedService(
        ObservableInstanceRegistry observableInstanceRegistry,
        IOptions<ModuleHostedServiceOption> options,
        IServiceScopeFactory serviceScopeFactory)
        : MoHostedService(
            observableInstanceRegistry,
            options,
            serviceScopeFactory,
            NullLogger<TestHostedService>.Instance)
    {
        public Task<string> RunWorkItemAsync(
            WorkItemInput input,
            CancellationToken cancellationToken)
        {
            return ExecuteWorkItemAsync<ScopedWorkItem, WorkItemInput, string>(
                input,
                cancellationToken);
        }
    }

    private sealed record WorkItemInput(string Value);

    private sealed class ScopeIdentity
    {
        public Guid Value { get; } = Guid.NewGuid();
    }

    private sealed class WorkItemCapture
    {
        public Guid PipelineScopeIdentity { get; set; }

        public Guid WorkItemScopeIdentity { get; set; }

        public ExecutionPoint? Point { get; set; }

        public HostedServiceExecutionFeature? Feature { get; set; }

        public bool WorkItemDisposed { get; set; }
    }

    private sealed class RecordingPipeline(
        ScopeIdentity identity,
        WorkItemCapture capture) : IExecutionPipeline
    {
        public Task<TResult> ExecuteAsync<TInput, TResult>(
            ExecutionContext<TInput> context,
            ExecutionDelegate<TResult> terminal)
        {
            capture.PipelineScopeIdentity = identity.Value;
            capture.Point = context.Descriptor.Point;
            capture.Feature = context.Features.GetRequired<HostedServiceExecutionFeature>();
            return terminal();
        }
    }

    private sealed class ScopedWorkItem(
        ScopeIdentity identity,
        WorkItemCapture capture) :
        IHostedServiceWorkItem<WorkItemInput, string>,
        IDisposable
    {
        public Task<string> ExecuteAsync(
            WorkItemInput input,
            CancellationToken cancellationToken)
        {
            capture.WorkItemScopeIdentity = identity.Value;
            return Task.FromResult(input.Value);
        }

        public void Dispose()
        {
            capture.WorkItemDisposed = true;
        }
    }
}
