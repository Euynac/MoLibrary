using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core.Execution;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Services;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;
using NSubstitute;
using Xunit;

namespace Test.Monica.JobScheduler.Services;

public sealed class JobExecutorExecutionPipelineTests
{
    [Fact]
    public async Task ExecuteRecurringJobAsync_ShouldWrapExactlyOneUserCodeAttempt()
    {
        var pipeline = new RecordingPipeline();
        await using var provider = CreateProvider(pipeline, services =>
            services.AddScoped<RecordingRecurringJob>());
        await using var scope = provider.CreateAsyncScope();
        var executor = CreateExecutor();

        await executor.ExecuteRecurringJobAsync(new JobExecutionContext
        {
            InstanceId = "recurring-1",
            ServiceProvider = scope.ServiceProvider,
            JobType = typeof(RecordingRecurringJob),
            CancellationToken = TestContext.Current.CancellationToken
        });

        pipeline.Invocations.Should().Be(1);
        pipeline.Point.Should().Be(JobSchedulerExecutionPoints.RecurringAttempt);
        pipeline.Feature.Should().Be(new JobExecutionFeature("recurring-1", JobType.Recurring));
        scope.ServiceProvider.GetRequiredService<RecordingRecurringJob>().Invocations.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteTriggeredJobAsync_WhenUserCodeFails_ShouldPreserveExceptionIdentity()
    {
        var expected = new TestJobException("failure");
        var pipeline = new RecordingPipeline();
        await using var provider = CreateProvider(pipeline, services =>
            services.AddTransient(_ => new FailingTriggeredJob(expected)));
        await using var scope = provider.CreateAsyncScope();
        var executor = CreateExecutor();
        var context = new JobExecutionContext
        {
            InstanceId = "triggered-1",
            ServiceProvider = scope.ServiceProvider,
            JobType = typeof(FailingTriggeredJob),
            JobArgs = new TestArguments("value"),
            CancellationToken = TestContext.Current.CancellationToken
        };

        Func<Task> execute = () => executor.ExecuteTriggeredJobAsync(context);

        var assertion = await execute.Should().ThrowAsync<TestJobException>();
        assertion.Which.Should().BeSameAs(expected);
        pipeline.Invocations.Should().Be(1);
        pipeline.Point.Should().Be(JobSchedulerExecutionPoints.TriggeredAttempt);
        pipeline.Feature.Should().Be(new JobExecutionFeature("triggered-1", JobType.Triggered));
    }

    private static ServiceProvider CreateProvider(
        IExecutionPipeline pipeline,
        Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => pipeline);
        configure(services);
        return services.BuildServiceProvider();
    }

    private static JobExecutor CreateExecutor()
    {
        var manager = new JobInstanceManager(
            Substitute.For<IJobMetadataRepository>(),
            Substitute.For<IEventBus>(),
            Substitute.For<IServiceDiscoveryClientInfo>(),
            Options.Create(new ModuleJobSchedulerOption()),
            NullLogger<JobInstanceManager>.Instance);
        return new JobExecutor(manager);
    }

    private sealed class RecordingPipeline : IExecutionPipeline
    {
        public int Invocations { get; private set; }

        public ExecutionPoint? Point { get; private set; }

        public JobExecutionFeature? Feature { get; private set; }

        public Task<TResult> ExecuteAsync<TInput, TResult>(
            ExecutionContext<TInput> context,
            ExecutionDelegate<TResult> terminal)
        {
            Invocations++;
            Point = context.Descriptor.Point;
            Feature = context.Features.GetRequired<JobExecutionFeature>();
            return terminal();
        }
    }

    private sealed class RecordingRecurringJob : IRecurringJob
    {
        public int Invocations { get; private set; }

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Invocations++;
            return Task.CompletedTask;
        }
    }

    private sealed record TestArguments(string Value);

    private sealed class FailingTriggeredJob(Exception exception) : ITriggeredJob<TestArguments>
    {
        public Task ExecuteAsync(TestArguments parameters, CancellationToken cancellationToken)
        {
            return Task.FromException(exception);
        }
    }

    private sealed class TestJobException(string message) : Exception(message);
}
