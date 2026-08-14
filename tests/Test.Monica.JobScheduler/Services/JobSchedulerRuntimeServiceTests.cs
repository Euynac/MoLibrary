using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity.Extensions;
using Monica.Core.ObservableInstance.Services;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Models.Operations;
using Monica.JobScheduler.Providers;
using Monica.JobScheduler.Services;
using Monica.JobScheduler.Services.Support;
using Monica.Modules;
using NSubstitute;
using Xunit;

namespace Test.Monica.JobScheduler.Services;

public sealed class JobSchedulerRuntimeServiceTests
{
    private const string SCOPE = "runtime-tests";
    private const string RELEASE = "release-1";
    private const string OWNER = "owner-a";
    private const string WORKER_REVISION = "worker-r1";
    private const string JOB_REVISION = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly DateTimeOffset NOW = new(2026, 8, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ControlPlaneConvergence_WhenOwnerPublishesWithoutWorkers_ShouldActivateAndBecomeReady()
    {
        var timeProvider = new ManualTimeProvider(NOW);
        var store = new InMemoryJobSchedulerStore(timeProvider);
        var identity = CreateIdentity(JobSchedulerRole.ControlPlane);
        var runtimeState = new JobSchedulerRuntimeState();
        var options = Options.Create(new ModuleJobSchedulerOption
        {
            Role = JobSchedulerRole.ControlPlane,
            EnableHistoryCleanup = false
        });
        await using var services = new ServiceCollection().BuildServiceProvider();
        using var service = new JobControlPlaneHostedService(
            store,
            identity,
            runtimeState,
            timeProvider,
            options,
            CreateObservableRegistry(),
            Options.Create(new ModuleHostedServiceOption()),
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobControlPlaneHostedService>.Instance);

        var awaiting = await service.ConvergeAsync(TestContext.Current.CancellationToken);
        var awaitingPublication = await store.GetCatalogPublicationStatusAsync(
            SCOPE,
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                SCOPE,
                RELEASE,
                OWNER,
                WORKER_REVISION,
                []),
            TestContext.Current.CancellationToken);
        var converged = await service.ConvergeAsync(TestContext.Current.CancellationToken);
        var active = await store.GetActiveCatalogAsync(
            SCOPE,
            TestContext.Current.CancellationToken);
        var workers = await store.GetActiveWorkerCapabilitiesAsync(
            SCOPE,
            cancellationToken: TestContext.Current.CancellationToken);

        awaiting.IsReady.Should().BeFalse();
        awaitingPublication.MissingOwnerIds.Should().Equal(OWNER);
        converged.IsReady.Should().BeTrue();
        runtimeState.ControlPlaneReady.Should().BeTrue();
        active.Should().NotBeNull();
        active!.Version.ActiveReleaseId.Should().Be(RELEASE);
        active.Version.ActiveIntentEpoch.Should().Be(active.Version.DesiredIntentEpoch);
        active.Definitions.Should().BeEmpty();
        workers.Should().BeEmpty();
    }

    [Fact]
    public async Task ControlPlaneConvergence_WhenAReplacementReleaseIsIncomplete_ShouldKeepActiveCatalogReady()
    {
        var timeProvider = new ManualTimeProvider(NOW);
        var store = new InMemoryJobSchedulerStore(timeProvider);
        var runtimeState = new JobSchedulerRuntimeState();
        var options = Options.Create(new ModuleJobSchedulerOption
        {
            Role = JobSchedulerRole.ControlPlane,
            EnableHistoryCleanup = false
        });
        await using var services = new ServiceCollection().BuildServiceProvider();
        using var service = new JobControlPlaneHostedService(
            store,
            CreateIdentity(JobSchedulerRole.ControlPlane),
            runtimeState,
            timeProvider,
            options,
            CreateObservableRegistry(),
            Options.Create(new ModuleHostedServiceOption()),
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobControlPlaneHostedService>.Instance);
        _ = await service.ConvergeAsync(TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(SCOPE, RELEASE, OWNER, WORKER_REVISION, []),
            TestContext.Current.CancellationToken);
        var initial = await service.ConvergeAsync(TestContext.Current.CancellationToken);
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(
                new JobCatalogReleaseManifest(
                    SCOPE,
                    "release-2",
                    [new JobCatalogOwnerManifest(OWNER, "worker-r2")]),
                2),
            TestContext.Current.CancellationToken);

        var transitioning = await service.ConvergeAsync(TestContext.Current.CancellationToken);

        initial.IsReady.Should().BeTrue();
        transitioning.IsReady.Should().BeTrue();
        transitioning.Message.Should().Contain("remains available on active release");
        transitioning.Message.Should().Contain("awaits activation");
        runtimeState.ControlPlaneReady.Should().BeTrue();
    }

    [Fact]
    public async Task ControlPlaneConvergence_WhenDebugModeChangesAcrossRestart_ShouldUpdateSameEpochSuspension()
    {
        var timeProvider = new ManualTimeProvider(NOW);
        var store = new InMemoryJobSchedulerStore(timeProvider);
        var options = Options.Create(new ModuleJobSchedulerOption
        {
            Role = JobSchedulerRole.ControlPlane,
            RecurringJobDebugMode = true,
            EnableHistoryCleanup = false
        });
        await using var services = new ServiceCollection().BuildServiceProvider();
        using var service = new JobControlPlaneHostedService(
            store,
            CreateIdentity(JobSchedulerRole.ControlPlane),
            new JobSchedulerRuntimeState(),
            timeProvider,
            options,
            CreateObservableRegistry(),
            Options.Create(new ModuleHostedServiceOption()),
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobControlPlaneHostedService>.Instance);
        _ = await service.ConvergeAsync(TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                SCOPE,
                RELEASE,
                OWNER,
                WORKER_REVISION,
                [CreateRecurringDeclaration("jobs.debug")]),
            TestContext.Current.CancellationToken);

        _ = await service.ConvergeAsync(TestContext.Current.CancellationToken);
        var summary = await store.GetOperationalSummaryAsync(
            SCOPE,
            "jobs.debug",
            TestContext.Current.CancellationToken);

        summary.Should().NotBeNull();
        summary!.Definition.IsDisabled.Should().BeFalse();
        summary.SuspensionReasons.Should().Be(JobRecurringScheduleSuspensionReason.DebugMode);
        summary.IsSuspended.Should().BeTrue();
        summary.RecurringScheduleStatus.Should().Be(JobRecurringScheduleStatus.Suspended);
        summary.NextOccurrenceUtc.Should().BeNull();

        using var resumedService = new JobControlPlaneHostedService(
            store,
            CreateIdentity(JobSchedulerRole.ControlPlane),
            new JobSchedulerRuntimeState(),
            timeProvider,
            Options.Create(new ModuleJobSchedulerOption
            {
                Role = JobSchedulerRole.ControlPlane,
                EnableHistoryCleanup = false
            }),
            CreateObservableRegistry(),
            Options.Create(new ModuleHostedServiceOption()),
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobControlPlaneHostedService>.Instance);

        _ = await resumedService.ConvergeAsync(TestContext.Current.CancellationToken);
        var resumed = await store.GetOperationalSummaryAsync(
            SCOPE,
            "jobs.debug",
            TestContext.Current.CancellationToken);

        resumed!.Definition.IsDisabled.Should().BeFalse();
        resumed.SuspensionReasons.Should().Be(JobRecurringScheduleSuspensionReason.None);
        resumed.IsSuspended.Should().BeFalse();
        resumed.RecurringScheduleStatus.Should().Be(JobRecurringScheduleStatus.Scheduled);
        resumed.NextOccurrenceUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ControlPlaneRecurringCatchUp_WhenSeveralCursorsAreOverdue_ShouldShareGlobalOccurrenceBudget()
    {
        var timeProvider = new ManualTimeProvider(NOW);
        var store = new InMemoryJobSchedulerStore(timeProvider);
        var options = Options.Create(new ModuleJobSchedulerOption
        {
            Role = JobSchedulerRole.ControlPlane,
            EnableHistoryCleanup = false,
            MaxRecurringMaterializationsPerCycle = 3
        });
        await using var services = new ServiceCollection().BuildServiceProvider();
        using var service = new JobControlPlaneHostedService(
            store,
            CreateIdentity(JobSchedulerRole.ControlPlane),
            new JobSchedulerRuntimeState(),
            timeProvider,
            options,
            CreateObservableRegistry(),
            Options.Create(new ModuleHostedServiceOption()),
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobControlPlaneHostedService>.Instance);
        _ = await service.ConvergeAsync(TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                SCOPE,
                RELEASE,
                OWNER,
                WORKER_REVISION,
                [CreateRecurringDeclaration("jobs.alpha"), CreateRecurringDeclaration("jobs.beta")]),
            TestContext.Current.CancellationToken);
        _ = await service.ConvergeAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        _ = await service.ConvergeAsync(TestContext.Current.CancellationToken);
        var firstCycle = await store.QueryExecutionsAsync(new JobExecutionQuery
        {
            SchedulerScopeKey = SCOPE,
            PageSize = 10
        }, TestContext.Current.CancellationToken);
        _ = await service.ConvergeAsync(TestContext.Current.CancellationToken);
        var secondCycle = await store.QueryExecutionsAsync(new JobExecutionQuery
        {
            SchedulerScopeKey = SCOPE,
            PageSize = 10
        }, TestContext.Current.CancellationToken);

        firstCycle.TotalCount.Should().Be(3);
        firstCycle.Items.Select(static execution => execution.Template.Revision.JobKey)
            .Distinct(StringComparer.Ordinal)
            .Should().HaveCount(2);
        secondCycle.TotalCount.Should().Be(6);
        secondCycle.Items.GroupBy(static execution => execution.Template.Revision.JobKey)
            .Select(static group => group.Count())
            .Should().OnlyContain(static count => count == 3);
    }

    [Fact]
    public async Task ControlPlaneCleanup_WhenBatchIsFull_ShouldContinueNextPollUntilAPartialBatch()
    {
        var timeProvider = new ManualTimeProvider(NOW);
        var store = Substitute.For<IJobSchedulerStore>();
        var version = new JobCatalogVersion(SCOPE, RELEASE, RELEASE, 1, 1, 1, 1, 1, 1);
        store.StageReleaseAsync(Arg.Any<JobCatalogReleaseStage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ReleaseStageResult(
                ReleaseStageStatus.Duplicate,
                RELEASE,
                "manifest-hash",
                version)));
        store.GetActiveCatalogAsync(SCOPE, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<JobCatalogSnapshot?>(new JobCatalogSnapshot(version, [])));
        store.GetDueRecurringSchedulesAsync(SCOPE, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RecurringScheduleCursor>>([]));
        store.RecoverExpiredLeasesAsync(Arg.Any<ExpiredLeaseRecoveryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<JobExecutionInstance>>([]));
        store.GetExecutionCleanupCandidatesAsync(
                SCOPE,
                Arg.Any<IReadOnlyDictionary<string, JobHistoryRetentionPolicy>>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<string>>(["history-0", "history-1"]),
                Task.FromResult<IReadOnlyList<string>>(["history-2"]));
        store.DeleteExecutionsAsync(SCOPE, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<IEnumerable<string>>().Count()));
        var options = Options.Create(new ModuleJobSchedulerOption
        {
            Role = JobSchedulerRole.ControlPlane,
            EnableHistoryCleanup = true,
            MaxHistoryDeletionsPerCycle = 2,
            HistoryCleanupInterval = TimeSpan.FromHours(1)
        });
        await using var services = new ServiceCollection().BuildServiceProvider();
        using var service = new JobControlPlaneHostedService(
            store,
            CreateIdentity(JobSchedulerRole.ControlPlane),
            new JobSchedulerRuntimeState(),
            timeProvider,
            options,
            CreateObservableRegistry(),
            Options.Create(new ModuleHostedServiceOption()),
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobControlPlaneHostedService>.Instance);

        await service.ConvergeAsync(TestContext.Current.CancellationToken);
        await service.ConvergeAsync(TestContext.Current.CancellationToken);
        await service.ConvergeAsync(TestContext.Current.CancellationToken);

        _ = store.Received(2).GetExecutionCleanupCandidatesAsync(
            SCOPE,
            Arg.Any<IReadOnlyDictionary<string, JobHistoryRetentionPolicy>>(),
            10,
            2,
            TestContext.Current.CancellationToken);
        _ = store.Received(2).DeleteExecutionsAsync(
            SCOPE,
            Arg.Any<IEnumerable<string>>(),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WorkerPublication_WhenOwnerHasNoJobs_ShouldRegisterAHealthyEmptyCapability()
    {
        var timeProvider = new ManualTimeProvider(NOW);
        var store = new InMemoryJobSchedulerStore(timeProvider);
        var identity = CreateIdentity(JobSchedulerRole.Worker);
        var schedulerOptions = Options.Create(new ModuleJobSchedulerOption
        {
            Role = JobSchedulerRole.Worker,
            WorkerCapabilityLeaseDuration = TimeSpan.FromMinutes(1),
            WorkerCapabilityRenewInterval = TimeSpan.FromSeconds(20)
        });
        await using var services = new ServiceCollection().BuildServiceProvider();
        var definitions = Array.Empty<LocalJobDefinition>();
        var registry = new JobRegistry(definitions, NullLogger<JobRegistry>.Instance);
        var orchestrator = new JobOrchestrator(
            services.GetRequiredService<IServiceScopeFactory>(),
            new JobExecutor(),
            registry,
            store,
            schedulerOptions,
            NullLogger<JobOrchestrator>.Instance);
        using var worker = new JobWorkerHostedService(
            store,
            identity,
            definitions,
            orchestrator,
            new JobSchedulerRuntimeState(),
            timeProvider,
            Substitute.For<IHostApplicationLifetime>(),
            schedulerOptions,
            CreateObservableRegistry(),
            Options.Create(new ModuleHostedServiceOption()),
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobWorkerHostedService>.Instance);

        await worker.EnsurePublishedAndRegisteredAsync(TestContext.Current.CancellationToken);
        var publication = await store.GetCatalogPublicationStatusAsync(
            SCOPE,
            TestContext.Current.CancellationToken);
        var capabilities = await store.GetActiveWorkerCapabilitiesAsync(
            SCOPE,
            cancellationToken: TestContext.Current.CancellationToken);

        publication.MissingOwnerIds.Should().BeEmpty();
        capabilities.Should().ContainSingle();
        capabilities[0].Capability.JobRevisionIds.Should().BeEmpty();
    }

    [Fact]
    public async Task RenewCapability_WhenStoreClockIsSkewed_ShouldUseLocalMonotonicInterval()
    {
        var timeProvider = new SkewedTimeProvider(NOW);
        var store = Substitute.For<IJobSchedulerStore>();
        var initialLease = CreateCapabilityLease(NOW.AddYears(10));
        var renewedLease = CreateCapabilityLease(NOW.AddYears(10).AddMinutes(1));
        store.StageReleaseAsync(Arg.Any<JobCatalogReleaseStage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReleaseStageResult>(null!));
        store.PublishOwnerSnapshotAsync(Arg.Any<JobOwnerCatalogSnapshot>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<OwnerSnapshotPublishResult>(null!));
        store.RegisterWorkerCapabilityAsync(
                Arg.Any<WorkerCapabilityRegistration>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(initialLease);
        store.RenewWorkerCapabilityAsync(
                Arg.Any<WorkerCapabilityLeaseKey>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(renewedLease);
        var schedulerOptions = Options.Create(new ModuleJobSchedulerOption
        {
            Role = JobSchedulerRole.Worker,
            WorkerCapabilityLeaseDuration = TimeSpan.FromMinutes(1),
            WorkerCapabilityRenewInterval = TimeSpan.FromSeconds(20)
        });
        await using var services = new ServiceCollection().BuildServiceProvider();
        var definitions = Array.Empty<LocalJobDefinition>();
        using var worker = new JobWorkerHostedService(
            store,
            CreateIdentity(JobSchedulerRole.Worker),
            definitions,
            new JobOrchestrator(
                services.GetRequiredService<IServiceScopeFactory>(),
                new JobExecutor(),
                new JobRegistry(definitions, NullLogger<JobRegistry>.Instance),
                store,
                schedulerOptions,
                NullLogger<JobOrchestrator>.Instance),
            new JobSchedulerRuntimeState(),
            timeProvider,
            Substitute.For<IHostApplicationLifetime>(),
            schedulerOptions,
            CreateObservableRegistry(),
            Options.Create(new ModuleHostedServiceOption()),
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobWorkerHostedService>.Instance);

        await worker.EnsurePublishedAndRegisteredAsync(TestContext.Current.CancellationToken);
        await worker.RenewCapabilityIfNeededAsync(TestContext.Current.CancellationToken);
        _ = store.DidNotReceive().RenewWorkerCapabilityAsync(
            Arg.Any<WorkerCapabilityLeaseKey>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
        timeProvider.SetUtcNow(NOW.AddDays(-1));
        timeProvider.AdvanceMonotonic(TimeSpan.FromSeconds(20));
        await worker.RenewCapabilityIfNeededAsync(TestContext.Current.CancellationToken);

        _ = store.Received(1).RenewWorkerCapabilityAsync(
            initialLease.LeaseKey,
            TimeSpan.FromMinutes(1),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteLease_WhenRenewalFails_ShouldCancelAndJoinUserCodeWithoutCompletingTheLease()
    {
        var probe = new CancellationProbe();
        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Services
            .AddSingleton(probe)
            .AddScoped<CancellationObservingRecurringJob>();
        hostBuilder.AddMonica(monica => monica.AddExecutionPipeline());
        using var host = hostBuilder.Build();

        var definition = new LocalJobDefinition
        {
            JobClrType = typeof(CancellationObservingRecurringJob),
            Declaration = new JobDeclaration
            {
                JobKey = typeof(CancellationObservingRecurringJob).FullName!,
                JobName = nameof(CancellationObservingRecurringJob),
                JobType = JobType.Recurring,
                MaxConcurrency = 1,
                MaxExecutionTimeout = TimeSpan.FromMinutes(1)
            }
        };
        var store = Substitute.For<IJobSchedulerStore>();
        var renewalFailure = new InvalidOperationException("renewal failed");
        store.RenewLeaseAsync(
                Arg.Any<JobLeaseKey>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<JobLeaseRenewalResult>(renewalFailure));
        var schedulerOptions = Options.Create(new ModuleJobSchedulerOption
        {
            Role = JobSchedulerRole.Worker,
            ExecutionLeaseDuration = TimeSpan.FromMinutes(1),
            ExecutionLeaseRenewInterval = TimeSpan.FromMilliseconds(1)
        });
        var orchestrator = new JobOrchestrator(
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            new JobExecutor(),
            new JobRegistry([definition], NullLogger<JobRegistry>.Instance),
            store,
            schedulerOptions,
            NullLogger<JobOrchestrator>.Instance);
        using var worker = new JobWorkerHostedService(
            store,
            CreateIdentity(JobSchedulerRole.Worker),
            [definition],
            orchestrator,
            new JobSchedulerRuntimeState(),
            TimeProvider.System,
            Substitute.For<IHostApplicationLifetime>(),
            schedulerOptions,
            CreateObservableRegistry(),
            Options.Create(new ModuleHostedServiceOption()),
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobWorkerHostedService>.Instance);

        var attempt = worker.ExecuteLeaseAsync(
            CreateLease(definition.Declaration.JobKey),
            TestContext.Current.CancellationToken);
        await probe.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => attempt.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        exception.Should().BeSameAs(renewalFailure);
        probe.Exited.Task.IsCompletedSuccessfully.Should().BeTrue();
        _ = store.DidNotReceive().CompleteAttemptAsync(
            Arg.Any<JobAttemptCompletion>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteLease_WhenTimedOutJobIgnoresCancellation_ShouldFenceLeaseAndRequestHostRestart()
    {
        var probe = new CancellationProbe();
        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Services
            .AddSingleton(probe)
            .AddScoped<CancellationIgnoringRecurringJob>();
        hostBuilder.AddMonica(monica => monica.AddExecutionPipeline());
        using var host = hostBuilder.Build();

        var executionTimeout = TimeSpan.FromMilliseconds(30);
        var definition = new LocalJobDefinition
        {
            JobClrType = typeof(CancellationIgnoringRecurringJob),
            Declaration = new JobDeclaration
            {
                JobKey = typeof(CancellationIgnoringRecurringJob).FullName!,
                JobName = nameof(CancellationIgnoringRecurringJob),
                JobType = JobType.Recurring,
                MaxConcurrency = 1,
                MaxExecutionTimeout = executionTimeout
            }
        };
        var store = Substitute.For<IJobSchedulerStore>();
        store.RenewLeaseAsync(
                Arg.Any<JobLeaseKey>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new JobLeaseRenewalResult { Status = JobLeaseRenewalStatus.Active });
        var applicationLifetime = Substitute.For<IHostApplicationLifetime>();
        var schedulerOptions = Options.Create(new ModuleJobSchedulerOption
        {
            Role = JobSchedulerRole.Worker,
            ExecutionLeaseDuration = TimeSpan.FromMinutes(1),
            ExecutionLeaseRenewInterval = TimeSpan.FromMilliseconds(5),
            ExecutionCancellationGracePeriod = TimeSpan.FromMilliseconds(30)
        });
        var orchestrator = new JobOrchestrator(
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            new JobExecutor(),
            new JobRegistry([definition], NullLogger<JobRegistry>.Instance),
            store,
            schedulerOptions,
            NullLogger<JobOrchestrator>.Instance);
        using var worker = new JobWorkerHostedService(
            store,
            CreateIdentity(JobSchedulerRole.Worker),
            [definition],
            orchestrator,
            new JobSchedulerRuntimeState(),
            TimeProvider.System,
            applicationLifetime,
            schedulerOptions,
            CreateObservableRegistry(),
            Options.Create(new ModuleHostedServiceOption()),
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobWorkerHostedService>.Instance);

        var attempt = worker.ExecuteLeaseAsync(
            CreateLease(definition.Declaration.JobKey, executionTimeout),
            TestContext.Current.CancellationToken);
        await probe.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await attempt.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        applicationLifetime.Received(1).StopApplication();
        _ = store.Received().RenewLeaseAsync(
            Arg.Any<JobLeaseKey>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
        _ = store.DidNotReceive().CompleteAttemptAsync(
            Arg.Any<JobAttemptCompletion>(),
            Arg.Any<CancellationToken>());
        _ = store.DidNotReceive().ReleaseLeaseAsync(
            Arg.Any<JobLeaseKey>(),
            Arg.Any<CancellationToken>());

        probe.Release.TrySetResult();
        await probe.Exited.Task.WaitAsync(TestContext.Current.CancellationToken);
    }

    private static JobSchedulerHostIdentity CreateIdentity(JobSchedulerRole role)
    {
        return new JobSchedulerHostIdentity
        {
            ReleaseStage = new JobCatalogReleaseStage(
                new JobCatalogReleaseManifest(
                    SCOPE,
                    RELEASE,
                    [new JobCatalogOwnerManifest(OWNER, WORKER_REVISION)]),
                1),
            LocalOwnerId = role == JobSchedulerRole.ControlPlane ? null : OWNER,
            LocalWorkerRevisionId = role == JobSchedulerRole.ControlPlane ? null : WORKER_REVISION,
            WorkerInstanceId = "worker-instance-1"
        };
    }

    private static ObservableInstanceRegistry CreateObservableRegistry() =>
        new(Options.Create(new ModuleObservableInstanceOption()));

    private static JobDeclaration CreateRecurringDeclaration(string jobKey) => new()
    {
        JobKey = jobKey,
        JobName = jobKey,
        JobType = JobType.Recurring,
        CronExpression = "0 * * * * *",
        TimeZoneId = TimeZoneInfo.Utc.Id,
        MaxConcurrency = 1,
        MaxExecutionTimeout = TimeSpan.FromMinutes(1)
    };

    private static JobExecutionLease CreateLease(
        string jobKey,
        TimeSpan? maxExecutionTimeout = null)
    {
        return new JobExecutionLease
        {
            Execution = new JobExecutionInstance
            {
                InstanceId = "execution-1",
                Template = new JobExecutionTemplate
                {
                    Revision = new JobRevisionIdentity
                    {
                        SchedulerScopeKey = SCOPE,
                        CatalogReleaseId = RELEASE,
                        ActivationEpoch = 1,
                        OwnerKey = OWNER,
                        WorkerRevisionId = WORKER_REVISION,
                        JobRevisionId = JOB_REVISION,
                        JobKey = jobKey
                    },
                    JobName = jobKey,
                    JobType = JobType.Recurring,
                    MaxConcurrency = 1,
                    MaxExecutionTimeout = maxExecutionTimeout ?? TimeSpan.FromMinutes(1)
                },
                AvailableAtUtc = NOW,
                State = JobExecutionState.Running,
                CreatedAtUtc = NOW
            },
            LeaseKey = new JobLeaseKey
            {
                SchedulerScopeKey = SCOPE,
                InstanceId = "execution-1",
                WorkerInstanceId = "worker-instance-1",
                LeaseToken = "lease-token"
            }
        };
    }

    private static WorkerCapabilityLease CreateCapabilityLease(DateTimeOffset expiresAtUtc)
    {
        var capability = new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = SCOPE,
            OwnerKey = OWNER,
            WorkerRevisionId = WORKER_REVISION,
            WorkerInstanceId = "worker-instance-1",
            JobRevisionIds = []
        };
        return new WorkerCapabilityLease
        {
            Capability = capability,
            LeaseKey = new WorkerCapabilityLeaseKey
            {
                SchedulerScopeKey = SCOPE,
                WorkerInstanceId = capability.WorkerInstanceId,
                LeaseToken = "capability-lease-token"
            },
            LeaseExpiresAtUtc = expiresAtUtc
        };
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private sealed class SkewedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override long GetTimestamp() => _timestamp;

        public void SetUtcNow(DateTimeOffset value) => _utcNow = value;

        public void AdvanceMonotonic(TimeSpan duration) => _timestamp += duration.Ticks;
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
