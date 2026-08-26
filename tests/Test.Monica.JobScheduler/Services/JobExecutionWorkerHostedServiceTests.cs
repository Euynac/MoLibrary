using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Models;
using Monica.Core.Modularity.Extensions;
using Monica.Core.ObservableInstance.Services;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Definitions;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Providers;
using Monica.JobScheduler.Services;
using Monica.JobScheduler.Services.Support;
using Monica.Modules;
using NSubstitute;
using Test.Monica.JobScheduler.Stores.Shared;
using Xunit;

namespace Test.Monica.JobScheduler.Services;

public sealed class JobExecutionWorkerHostedServiceTests
{
    private const string OWNER = StoreFixture.OWNER_A;
    private const string WORKER_INSTANCE = "worker-instance-1";
    private static readonly DateTimeOffset NOW = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ClaimAvailableWorkAsync_ShouldClaimAndExecuteOnlyLocalOwnerJobs()
    {
        var time = new StoreFixture.ManualTimeProvider(NOW);
        var store = new InMemoryJobSchedulerStore(time);
        var probe = new CancellationProbe();
        var definition = new LocalJobDefinition
        {
            JobClrType = typeof(CancellationObservingRecurringJob),
            Declaration = StoreFixture.RecurringDeclaration(
                typeof(CancellationObservingRecurringJob).FullName!,
                cron: "0 0 0 1 1 *")
        };
        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Services.AddSingleton(probe);
        hostBuilder.Services.AddScoped<CancellationObservingRecurringJob>();
        hostBuilder.AddMonica(monica => monica.AddExecutionPipeline());
        using var host = hostBuilder.Build();
        var (worker, _) = CreateWorker(
            store,
            [definition],
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            options => options.SchedulerScopeKey = StoreFixture.SCOPE);

        await store.SyncOwnerSnapshotAsync(new JobOwnerSnapshot(
            StoreFixture.SCOPE,
            OWNER,
            [definition.Declaration]), TestContext.Current.CancellationToken);
        await store.SyncOwnerSnapshotAsync(new JobOwnerSnapshot(
            StoreFixture.SCOPE,
            StoreFixture.OWNER_B,
            [StoreFixture.TriggeredDeclaration("jobs.other")]), TestContext.Current.CancellationToken);
        await store.RunRecurringNowAsync(new JobRecurringRunNowCommand
        {
            SchedulerScopeKey = StoreFixture.SCOPE,
            OwnerKey = OWNER,
            JobKey = definition.Declaration.JobKey,
            InstanceId = "own-exec"
        }, TestContext.Current.CancellationToken);
        await store.EnqueueAsync(new JobEnqueueRequest
        {
            SchedulerScopeKey = StoreFixture.SCOPE,
            OwnerKey = StoreFixture.OWNER_B,
            JobKey = "jobs.other",
            InstanceId = "other-exec",
            JobArgs = "{\"v\":1}"
        }, TestContext.Current.CancellationToken);

        using var workerStop = new CancellationTokenSource();
        await worker.ClaimAvailableWorkAsync(workerStop.Token);
        var running = await store.QueryExecutionsAsync(new JobExecutionQuery
        {
            SchedulerScopeKey = StoreFixture.SCOPE,
            States = [JobExecutionState.Running]
        }, TestContext.Current.CancellationToken);
        running.Items.Should().ContainSingle().Which.InstanceId.Should().Be("own-exec");

        await probe.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await workerStop.CancelAsync();
        // Cooperative job code observes the linked worker token during shutdown.
        await probe.Exited.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteLease_WhenLeaseRenewalIsLost_ShouldCancelCooperativeJobWithoutCompletion()
    {
        var probe = new CancellationProbe();
        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Services.AddSingleton(probe);
        hostBuilder.Services.AddScoped<CancellationObservingRecurringJob>();
        hostBuilder.AddMonica(monica => monica.AddExecutionPipeline());
        using var host = hostBuilder.Build();

        var definition = new LocalJobDefinition
        {
            JobClrType = typeof(CancellationObservingRecurringJob),
            Declaration = StoreFixture.RecurringDeclaration(
                typeof(CancellationObservingRecurringJob).FullName!)
        };
        var store = Substitute.For<IJobSchedulerStore>();
        store.RenewLeaseAsync(
                Arg.Any<JobLeaseKey>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new JobLeaseRenewalResult { Status = JobLeaseRenewalStatus.Lost });
        var (worker, options) = CreateWorker(
            store,
            [definition],
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            configure: null);

        var attempt = worker.ExecuteLeaseAsync(
            CreateLease(definition.Declaration.JobKey, options.Value.ExecutionLeaseDuration),
            TestContext.Current.CancellationToken);
        await probe.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await attempt.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await store.Received(1).RenewLeaseAsync(
            Arg.Any<JobLeaseKey>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().CompleteAttemptAsync(
            Arg.Any<JobAttemptCompletion>(),
            Arg.Any<CancellationToken>());
        probe.Exited.Task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteLease_WhenTimedOutJobIgnoresCancellation_ShouldFenceLeaseAndRequestHostRestart()
    {
        var probe = new CancellationProbe();
        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Services.AddSingleton(probe);
        hostBuilder.Services.AddScoped<CancellationIgnoringRecurringJob>();
        hostBuilder.AddMonica(monica => monica.AddExecutionPipeline());
        using var host = hostBuilder.Build();

        var executionTimeout = TimeSpan.FromMilliseconds(30);
        var definition = new LocalJobDefinition
        {
            JobClrType = typeof(CancellationIgnoringRecurringJob),
            Declaration = StoreFixture.RecurringDeclaration(
                typeof(CancellationIgnoringRecurringJob).FullName!)
        };
        var store = Substitute.For<IJobSchedulerStore>();
        store.RenewLeaseAsync(
                Arg.Any<JobLeaseKey>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new JobLeaseRenewalResult { Status = JobLeaseRenewalStatus.Active });
        var applicationLifetime = Substitute.For<IHostApplicationLifetime>();
        var (worker, options) = CreateWorker(
            store,
            [definition],
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            configure: schedulerOptions =>
            {
                schedulerOptions.ExecutionCancellationGracePeriod = TimeSpan.FromMilliseconds(30);
                schedulerOptions.ExecutionLeaseRenewInterval = TimeSpan.FromMilliseconds(5);
            },
            applicationLifetime);

        var attempt = worker.ExecuteLeaseAsync(
            CreateLease(definition.Declaration.JobKey, executionTimeout),
            TestContext.Current.CancellationToken);
        await probe.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await attempt.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        applicationLifetime.Received(1).StopApplication();
        await store.DidNotReceive().CompleteAttemptAsync(
            Arg.Any<JobAttemptCompletion>(),
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().ReleaseLeaseAsync(
            Arg.Any<JobLeaseKey>(),
            Arg.Any<CancellationToken>());

        probe.Release.TrySetResult();
        await probe.Exited.Task.WaitAsync(TestContext.Current.CancellationToken);
    }

    private static (JobExecutionWorkerHostedService Worker, OptionsWrapper<ModuleJobSchedulerOption> Options)
        CreateWorker(
            IJobSchedulerStore store,
            IReadOnlyList<LocalJobDefinition> definitions,
            IServiceScopeFactory scopeFactory,
            Action<ModuleJobSchedulerOption>? configure,
            IHostApplicationLifetime? applicationLifetime = null)
    {
        var options = new OptionsWrapper<ModuleJobSchedulerOption>(new ModuleJobSchedulerOption
        {
            SchedulerScopeKey = StoreFixture.SCOPE,
            ProjectName = OWNER,
            WorkerInstanceId = WORKER_INSTANCE,
            ExecutionLeaseDuration = TimeSpan.FromMinutes(1),
            ExecutionLeaseRenewInterval = TimeSpan.FromMilliseconds(5),
            ExecutionCancellationGracePeriod = TimeSpan.FromSeconds(5)
        });
        configure?.Invoke(options.Value);
        var schedulerOptions = Options.Create(options.Value);
        var orchestrator = new JobOrchestrator(
            scopeFactory,
            new JobExecutor(),
            new JobRegistry(definitions, NullLogger<JobRegistry>.Instance),
            store,
            schedulerOptions,
            NullLogger<JobOrchestrator>.Instance);
        var worker = new JobExecutionWorkerHostedService(
            store,
            definitions,
            orchestrator,
            new JobSchedulerRuntimeState(),
            TimeProvider.System,
            applicationLifetime ?? Substitute.For<IHostApplicationLifetime>(),
            schedulerOptions,
            new ObservableInstanceRegistry(Options.Create(new ModuleObservableInstanceOption())),
            Options.Create(new ModuleHostedServiceOption()),
            scopeFactory,
            NullLogger<JobExecutionWorkerHostedService>.Instance);
        return (worker, options);
    }

    private static JobExecutionLease CreateLease(string jobKey, TimeSpan executionTimeout)
    {
        return new JobExecutionLease
        {
            Execution = new JobExecutionInstance
            {
                InstanceId = "execution-1",
                Template = new JobExecutionTemplate
                {
                    SchedulerScopeKey = StoreFixture.SCOPE,
                    OwnerKey = OWNER,
                    JobKey = jobKey,
                    JobName = jobKey,
                    JobType = JobType.Recurring,
                    MaxConcurrency = 1,
                    MaxExecutionTimeout = executionTimeout
                },
                AvailableAtUtc = NOW,
                State = JobExecutionState.Running,
                CreatedAtUtc = NOW
            },
            LeaseKey = new JobLeaseKey
            {
                SchedulerScopeKey = StoreFixture.SCOPE,
                InstanceId = "execution-1",
                WorkerInstanceId = WORKER_INSTANCE,
                LeaseToken = "lease-token"
            }
        };
    }

    private sealed class CancellationProbe
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class CancellationObservingRecurringJob(CancellationProbe probe) : IRecurringJob
    {
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            probe.Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally
            {
                probe.Exited.TrySetResult();
            }
        }
    }

    private sealed class CancellationIgnoringRecurringJob(CancellationProbe probe) : IRecurringJob
    {
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            probe.Started.TrySetResult();
            try
            {
                await probe.Release.Task;
            }
            finally
            {
                probe.Exited.TrySetResult();
            }
        }
    }
}
