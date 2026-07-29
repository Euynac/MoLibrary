using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

    [Fact]
    public async Task ExecuteAsync_WhenHostOperationIsCancelled_ShouldCancelJobAndMarkInstanceCancelled()
    {
        var control = new CancellationAwareJobControl();
        using var jobCancellation = new CancellationTokenSource();
        using var hostCancellation = new CancellationTokenSource();
        var cancellationManager = CreateCancellationManager(jobCancellation);
        using var harness = await CreateHarnessAsync<CancellationAwareRecurringJob>(
            services => services
                .AddSingleton(control)
                .AddScoped<CancellationAwareRecurringJob>(),
            cancellationManager,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMilliseconds(50));

        var execution = harness.Orchestrator.ExecuteAsync(
            harness.Instance,
            harness.ExecutionEvent,
            hostCancellation.Token);
        await control.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await hostCancellation.CancelAsync();

        Func<Task> waitForExecution = () => execution;
        await waitForExecution.Should().ThrowAsync<OperationCanceledException>();

        harness.Instance.State.Should().Be(JobState.Cancelled);
        harness.Instance.StateHistory.Should().NotBeNull();
        harness.Instance.StateHistory!
            .Contains("timeout", StringComparison.OrdinalIgnoreCase)
            .Should().BeFalse();
        await cancellationManager.Received(1).CancelJobTokenAsync(
            harness.Instance.InstanceId,
            CancellationToken.None);
        await cancellationManager.Received(1).DeleteJobTokenAsync(
            harness.Instance.InstanceId,
            CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTimedOutJobFailsLate_ShouldObserveFailureBeforeDeletingJobToken()
    {
        var control = new LateFailingJobControl();
        using var jobCancellation = new CancellationTokenSource();
        var tokenDeleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationManager = CreateCancellationManager(jobCancellation);
        cancellationManager
            .DeleteJobTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                tokenDeleted.TrySetResult();
                return Task.CompletedTask;
            });
        var logger = new RecordingLogger<JobOrchestrator>();
        using var harness = await CreateHarnessAsync<LateFailingRecurringJob>(
            services => services
                .AddSingleton(control)
                .AddScoped<LateFailingRecurringJob>(),
            cancellationManager,
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(10),
            logger);

        await harness.Orchestrator.ExecuteAsync(
            harness.Instance,
            harness.ExecutionEvent,
            TestContext.Current.CancellationToken);

        harness.Instance.State.Should().Be(JobState.Failed);
        cancellationManager.ReceivedCalls()
            .Should().NotContain(call =>
                call.GetMethodInfo().Name == nameof(IJobCancellationTokenManager.DeleteJobTokenAsync));
        var drain = harness.Orchestrator.DrainLateExecutionsAsync(TestContext.Current.CancellationToken);
        var release = harness.Orchestrator.WaitForExecutionReleaseAsync(harness.Instance.InstanceId);
        drain.IsCompleted.Should().BeFalse();
        release.IsCompleted.Should().BeFalse();

        control.Release.TrySetResult();
        await drain;
        await release;
        await tokenDeleted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cancellationManager.Received(1).DeleteJobTokenAsync(
            harness.Instance.InstanceId,
            CancellationToken.None);
        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Error
            && entry.Exception is InvalidOperationException
            && entry.Message.Contains("failed late", StringComparison.Ordinal));
    }

    private static JobDefinition CreateDefinition(Type? jobType = null)
    {
        jobType ??= typeof(AsyncOnlyDisposableRecurringJob);
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

    private static IJobCancellationTokenManager CreateCancellationManager(
        CancellationTokenSource jobCancellation)
    {
        var cancellationManager = Substitute.For<IJobCancellationTokenManager>();
        cancellationManager
            .GetOrCreateJobTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(jobCancellation.Token);
        cancellationManager
            .CancelJobTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                jobCancellation.Cancel();
                return Task.CompletedTask;
            });
        cancellationManager
            .DeleteJobTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return cancellationManager;
    }

    private static async Task<JobOrchestratorHarness> CreateHarnessAsync<TJob>(
        Action<IServiceCollection> registerJob,
        IJobCancellationTokenManager cancellationManager,
        TimeSpan executionTimeout,
        TimeSpan cancellationGracePeriod,
        ILogger<JobOrchestrator>? logger = null)
        where TJob : class, IRecurringJob
    {
        var builder = Host.CreateApplicationBuilder();
        registerJob(builder.Services);
        builder.AddMonica(monica => monica.AddExecutionPipeline());
        var host = builder.Build();
        var options = Options.Create(new ModuleJobSchedulerOption
        {
            SchedulerScopeKey = SCHEDULER_SCOPE,
            ExecutionCancellationGracePeriod = cancellationGracePeriod
        });
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
            Substitute.For<IEventBus>(),
            clientInfo,
            options,
            NullLogger<JobInstanceManager>.Instance);
        var registry = new JobRegistry(
            Substitute.For<IJobDefinitionCacheService>(),
            repository,
            options,
            NullLogger<JobRegistry>.Instance);
        var definition = CreateDefinition(typeof(TJob));
        await registry.RegisterJob(definition, "test");
        var instance = CreateInstance(definition);
        await repository.SaveInstanceAsync(instance, TestContext.Current.CancellationToken);
        var orchestrator = new JobOrchestrator(
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            instanceManager,
            cancellationManager,
            new JobExecutor(instanceManager),
            registry,
            options,
            logger ?? NullLogger<JobOrchestrator>.Instance);
        var executionEvent = CreateExecutionEvent(definition, instance, executionTimeout);
        return new JobOrchestratorHarness(host, orchestrator, instance, executionEvent);
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
        JobInstance instance,
        TimeSpan? executionTimeout = null)
    {
        return new JobExecutionEvent
        {
            SchedulerScopeKey = SCHEDULER_SCOPE,
            InstanceId = instance.InstanceId,
            JobKey = definition.JobKey,
            JobType = JobType.Recurring,
            RequestedAt = DateTime.UtcNow,
            MaxExecutionTimeout = executionTimeout ?? TimeSpan.FromMinutes(1)
        };
    }

    private sealed record JobOrchestratorHarness(
        IHost Host,
        JobOrchestrator Orchestrator,
        JobInstance Instance,
        JobExecutionEvent ExecutionEvent) : IDisposable
    {
        public void Dispose() => Host.Dispose();
    }

    private sealed class CancellationAwareJobControl
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class CancellationAwareRecurringJob(CancellationAwareJobControl control) : IRecurringJob
    {
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            control.Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class LateFailingJobControl
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class LateFailingRecurringJob(LateFailingJobControl control) : IRecurringJob
    {
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await control.Release.Task;
            throw new InvalidOperationException("late job failure");
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Enqueue(new LogEntry(logLevel, exception, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);

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
