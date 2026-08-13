using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity.Abstractions;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Models;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Annotations;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Services;
using Monica.JobScheduler.Utils;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;
using Monica.Testing.Hosting;
using Xunit;

namespace Test.Monica.JobScheduler.Hosting;

public sealed class WorkerJobSchedulerExecutionTests
{
    private const string PROJECT_NAME = "Test.Monica.JobScheduler.Worker";
    private const string SCHEDULER_SCOPE = "worker-execution-tests";
    private static readonly TimeSpan HANG_GUARD = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task CreateAsync_WhenWorkerReceivesExecutionEvent_ShouldExecuteItsLocalJobType()
    {
        var factory = new WorkerJobSchedulerApplicationFactory();
        await using var application = await factory.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        var services = application.Services;
        var subscriptionRegistry = services.GetRequiredService<IEventSubscriptionRegistry>();
        var topic = JobEventTopicHelper.GetProjectTopicName<JobExecutionEvent>(
            SCHEDULER_SCOPE,
            PROJECT_NAME);

        await WaitFor.UntilAsync(
            _ => Task.FromResult(subscriptionRegistry.GetByTopicName(topic).Any(subscription =>
                subscription.EventType == typeof(JobExecutionEvent)
                && subscription.State == EventSubscriptionState.Active)),
            HANG_GUARD,
            cancellationToken: TestContext.Current.CancellationToken);

        services.GetRequiredService<IOptions<ModuleServiceDiscoveryOption>>().Value.Role
            .Should().Be(ServiceDiscoveryRole.Worker);
        services.GetRequiredService<ILeaderElectionService>().IsLeader.Should().BeFalse();

        var definition = services.GetRequiredService<IReadOnlyList<JobDefinition>>()
            .Single(candidate => candidate.JobClrType == typeof(WorkerExecutionProbeJob));
        var instance = await services.GetRequiredService<JobInstanceManager>().CreateInstanceAsync(
            definition,
            parameters: null,
            initialState: JobState.Enqueued,
            cancellationToken: TestContext.Current.CancellationToken);
        var executionEvent = new JobExecutionEvent
        {
            SchedulerScopeKey = SCHEDULER_SCOPE,
            InstanceId = instance.InstanceId,
            JobKey = definition.JobKey,
            JobType = JobType.Recurring,
            RequestedAt = DateTime.UtcNow,
            MaxExecutionTimeout = HANG_GUARD
        };

        await services.GetRequiredKeyedService<IEventBus>(nameof(ModuleJobScheduler))
            .PublishAsync(executionEvent, topic, TestContext.Current.CancellationToken);

        var probe = services.GetRequiredService<WorkerExecutionProbe>();
        await probe.Executed.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        await WaitFor.UntilAsync(
            async cancellationToken =>
                (await services.GetRequiredService<IJobMetadataRepository>()
                    .GetInstanceAsync(instance.InstanceId, cancellationToken))?.State == JobState.Succeeded,
            HANG_GUARD,
            cancellationToken: TestContext.Current.CancellationToken);

        probe.ExecutionCount.Should().Be(1);
    }

    private sealed class WorkerJobSchedulerApplicationFactory
        : MonicaTestApplicationFactory<WorkerExecutionProbeJob>
    {
        protected override void ConfigureHost(WebApplicationBuilder builder)
        {
            builder.Services.AddSingleton<WorkerExecutionProbe>();
        }

        protected override void ConfigureMonica(IMonicaBuilder builder)
        {
            builder.AddEventBus(options => options.DisableAutoDiscovery = true);
            builder.AddServiceDiscovery()
                .AsWorker()
                .UseMemoryStorage();
            builder.AddJobScheduler(options =>
                {
                    options.ProjectName = PROJECT_NAME;
                    options.RecurringJobDebugMode = true;
                    options.TriggeredJobDebugMode = true;
                    options.EnableLongIntervalScheduler = false;
                    options.EnableZombieDetection = false;
                    options.EnableHistoryCleanup = false;
                })
                .UseSchedulerScope(SCHEDULER_SCOPE)
                .UseInMemoryProvider()
                .UseInMemoryMetadataRepository();
        }
    }
}

public sealed class WorkerExecutionProbe
{
    private readonly TaskCompletionSource _executed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _executionCount;

    public Task Executed => _executed.Task;

    public int ExecutionCount => Volatile.Read(ref _executionCount);

    public void RecordExecution()
    {
        Interlocked.Increment(ref _executionCount);
        _executed.TrySetResult();
    }
}

[JobConfig(CronSchedule = "0 0 0 * * *")]
public sealed class WorkerExecutionProbeJob(WorkerExecutionProbe probe) : IRecurringJob
{
    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        probe.RecordExecution();
        return Task.CompletedTask;
    }
}
