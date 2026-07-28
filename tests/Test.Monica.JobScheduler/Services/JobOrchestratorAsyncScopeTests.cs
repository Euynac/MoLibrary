using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core.Execution;
using Monica.Core.Modularity.Extensions;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Providers;
using Monica.JobScheduler.Services;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Models;
using NSubstitute;
using Xunit;

namespace Test.Monica.JobScheduler.Services;

public sealed class JobOrchestratorAsyncScopeTests
{
    private const string SCHEDULER_SCOPE = "async-scope-tests";

    [Fact]
    public async Task ExecuteAsync_WhenJobScopeContainsAsyncOnlyDisposables_ShouldAwaitDisposalAndRemainSucceeded()
    {
        var trace = new List<string>();
        using var host = BuildExecutionHost(trace);
        var options = Options.Create(new ModuleJobSchedulerOption
        {
            SchedulerScopeKey = SCHEDULER_SCOPE
        });
        var eventBus = Substitute.For<IEventBus>();
        var repository = new InMemoryJobMetadataRepository(
            NullLogger<InMemoryJobMetadataRepository>.Instance,
            options);
        var clientInfo = Substitute.For<IServiceDiscoveryClientInfo>();
        clientInfo.GetServiceStatus(Arg.Any<bool>()).Returns(new InstanceState
        {
            AppId = "test-app",
            InstanceId = "test-instance",
            AppName = "Test App",
            ProjectName = "Test.Project"
        });
        var instanceManager = new JobInstanceManager(
            repository,
            eventBus,
            clientInfo,
            options,
            NullLogger<JobInstanceManager>.Instance);
        var registry = new JobRegistry(
            Substitute.For<IJobDefinitionCacheService>(),
            repository,
            options,
            NullLogger<JobRegistry>.Instance);
        var definition = CreateDefinition();
        await registry.RegisterJob(definition, "test");
        var instance = CreateInstance(definition);
        await repository.SaveInstanceAsync(instance, TestContext.Current.CancellationToken);
        var cancellationManager = Substitute.For<IJobCancellationTokenManager>();
        cancellationManager
            .GetOrCreateJobTokenAsync(instance.InstanceId, Arg.Any<CancellationToken>())
            .Returns(CancellationToken.None);
        var orchestrator = new JobOrchestrator(
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            instanceManager,
            cancellationManager,
            new JobExecutor(instanceManager),
            registry,
            options,
            NullLogger<JobOrchestrator>.Instance);

        await orchestrator.ExecuteAsync(
            instance,
            CreateExecutionEvent(definition, instance),
            TestContext.Current.CancellationToken);

        instance.State.Should().Be(JobState.Succeeded);
        trace.Should().ContainInOrder(
            "behavior:enter",
            "job:execute",
            "behavior:exit");
        trace.IndexOf("behavior:disposed").Should().BeGreaterThan(trace.IndexOf("behavior:exit"));
        trace.IndexOf("job:disposed").Should().BeGreaterThan(trace.IndexOf("job:execute"));
    }

    private static JobDefinition CreateDefinition()
    {
        var jobType = typeof(AsyncOnlyDisposableRecurringJob);
        return new JobDefinition
        {
            SchedulerScopeKey = SCHEDULER_SCOPE,
            JobKey = jobType.FullName!,
            FromProject = "Test.Project",
            JobName = "Async disposal job",
            JobType = JobType.Recurring,
            JobClrType = jobType
        };
    }

    private static IHost BuildExecutionHost(List<string> trace)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddSingleton(trace)
            .AddScoped<AsyncOnlyDisposableRecurringJob>();
        builder.AddMonica(monica => monica
            .AddExecutionPipeline()
            .AddBehavior<AsyncOnlyDisposableJobBehavior>(
                descriptorFilter: static descriptor =>
                    descriptor.Point == JobSchedulerExecutionPoints.RecurringAttempt,
                lifetime: ServiceLifetime.Scoped));
        return builder.Build();
    }

    private static JobInstance CreateInstance(JobDefinition definition)
    {
        return new JobInstance
        {
            SchedulerScopeKey = SCHEDULER_SCOPE,
            InstanceId = "async-disposal-instance",
            JobKey = definition.JobKey,
            State = JobState.Enqueued,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static JobExecutionEvent CreateExecutionEvent(
        JobDefinition definition,
        JobInstance instance)
    {
        return new JobExecutionEvent
        {
            SchedulerScopeKey = SCHEDULER_SCOPE,
            InstanceId = instance.InstanceId,
            JobKey = definition.JobKey,
            JobType = JobType.Recurring,
            RequestedAt = DateTime.UtcNow,
            MaxExecutionTimeout = TimeSpan.FromMinutes(1)
        };
    }

    private sealed class AsyncOnlyDisposableRecurringJob(List<string> trace) :
        IRecurringJob,
        IAsyncDisposable
    {
        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            trace.Add("job:execute");
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            trace.Add("job:disposed");
        }
    }

    private sealed class AsyncOnlyDisposableJobBehavior(List<string> trace) :
        IExecutionBehavior<ExecutionUnit, ExecutionUnit>,
        IAsyncDisposable
    {
        public async Task<ExecutionUnit> ExecuteAsync(
            ExecutionContext<ExecutionUnit> context,
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
}
