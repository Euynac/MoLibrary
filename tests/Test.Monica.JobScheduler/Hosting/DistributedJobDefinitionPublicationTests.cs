using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity.Abstractions;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Models;
using Monica.EventBus.Services.Support;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Annotations;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Services.Support;
using Monica.JobScheduler.Utils;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;
using Monica.Testing.Doubles;
using Monica.Testing.Hosting;
using Xunit;

namespace Test.Monica.JobScheduler.Hosting;

public sealed class DistributedJobDefinitionPublicationTests
{
    private const string REGISTRY_PROJECT = "Test.Monica.JobScheduler.Registry";
    private const string SCHEDULER_SCOPE = "distributed-definition-publication-tests";
    private const string WORKER_PROJECT = "Test.Monica.JobScheduler.DefinitionWorker";
    private static readonly TimeSpan HANG_GUARD = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task WorkerAndRegistry_WhenCatalogStartsEmpty_ShouldPublishAndReconcileWorkerDefinition()
    {
        var broker = new SharedJobSchedulerEventBroker();
        var registryFactory = new RegistryApplicationFactory(broker);
        var workerFactory = new WorkerApplicationFactory(broker);

        await using var registry = await registryFactory.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        await WaitFor.UntilAsync(
            _ => Task.FromResult(registry.Services.GetRequiredService<ILeaderElectionService>().IsLeader),
            HANG_GUARD,
            cancellationToken: TestContext.Current.CancellationToken);
        await WaitFor.UntilAsync(
            _ => Task.FromResult(registry.Services.GetRequiredService<IEventSubscriptionRegistry>()
                .GetByTopicName(JobEventTopicHelper.GetTopicName<JobDefinitionSnapshotPublishedEvent>(SCHEDULER_SCOPE))
                .Any(static subscription => subscription.State == EventSubscriptionState.Active)),
            HANG_GUARD,
            cancellationToken: TestContext.Current.CancellationToken);

        await using var worker = await workerFactory.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        var repository = registry.Services.GetRequiredService<IJobMetadataRepository>();
        var jobKey = typeof(DefinitionPublicationProbeJob).FullName!;
        await WaitFor.UntilAsync(
            async cancellationToken =>
                await repository.GetDefinitionAsync(jobKey, cancellationToken) is
                {
                    FromProject: WORKER_PROJECT,
                    IsDeleted: false
                },
            HANG_GUARD,
            cancellationToken: TestContext.Current.CancellationToken);

        registry.Services.GetRequiredService<IReadOnlyList<JobDefinition>>().Should().BeEmpty();
        registry.Services.GetRequiredService<ILeaderElectionService>().IsLeader.Should().BeTrue();
        worker.Services.GetRequiredService<ILeaderElectionService>().IsLeader.Should().BeFalse();
        broker.PublishedSnapshots.Should().BeGreaterThan(0);

        var persisted = await repository.GetDefinitionAsync(
            jobKey,
            TestContext.Current.CancellationToken);
        persisted.Should().NotBeNull();
        persisted!.FromProject.Should().Be(WORKER_PROJECT);
        persisted.JobName.Should().Be("Definition publication probe");
        persisted.MaxConcurrency.Should().Be(3);
    }

    [Fact]
    public async Task Worker_WhenRegistryStartsAfterInitialPublication_ShouldRepublishAuthoritativeSnapshot()
    {
        var broker = new SharedJobSchedulerEventBroker();
        var workerFactory = new WorkerApplicationFactory(broker);

        await using var worker = await workerFactory.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        await WaitFor.UntilAsync(
            _ => Task.FromResult(broker.PublishedSnapshots > 0),
            HANG_GUARD,
            cancellationToken: TestContext.Current.CancellationToken);
        var publicationsBeforeRegistryStartup = broker.PublishedSnapshots;

        var registryFactory = new RegistryApplicationFactory(broker);
        await using var registry = await registryFactory.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        await WaitFor.UntilAsync(
            _ => Task.FromResult(registry.Services.GetRequiredService<ILeaderElectionService>().IsLeader),
            HANG_GUARD,
            cancellationToken: TestContext.Current.CancellationToken);
        await WaitFor.UntilAsync(
            _ => Task.FromResult(registry.Services.GetRequiredService<IEventSubscriptionRegistry>()
                .GetByTopicName(JobEventTopicHelper.GetTopicName<JobDefinitionSnapshotPublishedEvent>(SCHEDULER_SCOPE))
                .Any(static subscription => subscription.State == EventSubscriptionState.Active)),
            HANG_GUARD,
            cancellationToken: TestContext.Current.CancellationToken);

        var repository = registry.Services.GetRequiredService<IJobMetadataRepository>();
        var jobKey = typeof(DefinitionPublicationProbeJob).FullName!;
        await WaitFor.UntilAsync(
            async cancellationToken =>
                await repository.GetDefinitionAsync(jobKey, cancellationToken) is
                {
                    FromProject: WORKER_PROJECT,
                    IsDeleted: false
                },
            HANG_GUARD,
            cancellationToken: TestContext.Current.CancellationToken);

        broker.PublishedSnapshots.Should().BeGreaterThan(publicationsBeforeRegistryStartup);
        worker.Services.GetRequiredService<ILeaderElectionService>().IsLeader.Should().BeFalse();
    }

    private static void ConfigureMonica(
        IMonicaBuilder builder,
        string projectName,
        ServiceDiscoveryRole role)
    {
        builder.ConfigureApplication(options =>
        {
            options.ProjectName = projectName;
            options.AppId = projectName;
            options.AppName = projectName;
            options.AppVersion = "test";
        });
        builder.AddEventBus(options => options.DisableAutoDiscovery = true)
            .UseDistributedEventBus<SharedJobSchedulerEventBus>();
        builder.AddStateStore()
            .SetCommonDistributedStateStoreProvider<InMemoryDistributedStateStore>();

        var discovery = builder.AddServiceDiscovery(options =>
            {
                options.ProjectName = projectName;
                options.AppId = projectName;
                options.AppName = projectName;
                options.FromInstance = projectName + "-instance";
                options.BuildTime = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
                options.ReleaseVersion = "test";
                options.RegistrationWaitTimeout = HANG_GUARD;
                options.Election.HeartbeatPeriodSeconds = 1;
                options.Election.HeartbeatJitterMilliseconds = 0;
            })
            .UseMemoryStorage();
        if (role == ServiceDiscoveryRole.Registry)
        {
            discovery.AsRegistry();
        }
        else
        {
            discovery.AsWorker();
        }

        builder.AddJobScheduler(options =>
            {
                options.ProjectName = projectName;
                options.RecurringJobDebugMode = true;
                options.TriggeredJobDebugMode = true;
                options.EnableLongIntervalScheduler = false;
                options.EnableZombieDetection = false;
                options.EnableHistoryCleanup = false;
                options.DefinitionPublicationInterval = TimeSpan.FromMilliseconds(100);
                options.DefinitionPublicationRetryInterval = TimeSpan.FromMilliseconds(20);
            })
            .UseSchedulerScope(SCHEDULER_SCOPE)
            .UseDistributedProvider()
            .UseInMemoryMetadataRepository();
    }

    private abstract class DistributedApplicationFactory<TDiscoveryAnchor>(
        SharedJobSchedulerEventBroker broker)
        : MonicaTestApplicationFactory<TDiscoveryAnchor>
    {
        protected override void ConfigureHost(WebApplicationBuilder builder)
        {
            builder.Services.AddSingleton(broker);
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            services.RemoveAll<IDistributedEventBus>();
            services.AddSingleton<IDistributedEventBus>(provider =>
                provider.GetRequiredService<SharedJobSchedulerEventBus>());
        }
    }

    private sealed class RegistryApplicationFactory(SharedJobSchedulerEventBroker broker)
        : DistributedApplicationFactory<ModuleJobScheduler>(broker)
    {
        protected override void ConfigureMonica(IMonicaBuilder builder)
        {
            DistributedJobDefinitionPublicationTests.ConfigureMonica(
                builder,
                REGISTRY_PROJECT,
                ServiceDiscoveryRole.Registry);
        }
    }

    private sealed class WorkerApplicationFactory(SharedJobSchedulerEventBroker broker)
        : DistributedApplicationFactory<DefinitionPublicationProbeJob>(broker)
    {
        protected override void ConfigureMonica(IMonicaBuilder builder)
        {
            DistributedJobDefinitionPublicationTests.ConfigureMonica(
                builder,
                WORKER_PROJECT,
                ServiceDiscoveryRole.Worker);
        }
    }
}

[JobConfig(
    JobName = "Definition publication probe",
    CronSchedule = "0 0 0 * * *",
    MaxConcurrency = 3)]
public sealed class DefinitionPublicationProbeJob : IRecurringJob
{
    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

internal sealed class SharedJobSchedulerEventBroker
{
    private readonly ConcurrentDictionary<Guid, SharedJobSchedulerEventBus> _participants = new();
    private int _publishedSnapshots;

    internal int PublishedSnapshots => Volatile.Read(ref _publishedSnapshots);

    internal Guid Register(SharedJobSchedulerEventBus eventBus)
    {
        var participantId = Guid.NewGuid();
        if (!_participants.TryAdd(participantId, eventBus))
        {
            throw new InvalidOperationException("Could not register the shared test event-bus participant.");
        }

        return participantId;
    }

    internal void Unregister(Guid participantId)
    {
        _participants.TryRemove(participantId, out _);
    }

    internal async Task PublishAsync(
        Type eventType,
        object eventData,
        string topicName,
        CancellationToken cancellationToken)
    {
        if (eventType == typeof(JobDefinitionSnapshotPublishedEvent))
        {
            Interlocked.Increment(ref _publishedSnapshots);
        }

        foreach (var participant in _participants.Values.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await participant.DeliverAsync(eventType, eventData, topicName, cancellationToken);
        }
    }
}

internal sealed class SharedJobSchedulerEventBus : DistributedEventBusBase, IAsyncDisposable
{
    private readonly SharedJobSchedulerEventBroker _broker;
    private readonly Guid _participantId;

    public SharedJobSchedulerEventBus(
        IServiceScopeFactory serviceScopeFactory,
        IEventHandlerInvoker eventHandlerInvoker,
        IEventSubscriptionRegistry subscriptionRegistry,
        ILoggerFactory loggerFactory,
        SharedJobSchedulerEventBroker broker)
        : base(serviceScopeFactory, eventHandlerInvoker, subscriptionRegistry, loggerFactory)
    {
        _broker = broker;
        _participantId = broker.Register(this);
    }

    public override Task PublishAsync(
        Type eventType,
        object eventData,
        string? topicName = null,
        CancellationToken cancellationToken = default)
    {
        return _broker.PublishAsync(
            eventType,
            eventData,
            ResolveTopicName(eventType, topicName),
            cancellationToken);
    }

    public override async Task BulkPublishAsync(
        Type eventType,
        IEnumerable<object> eventDataList,
        string? topicName = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTopic = ResolveTopicName(eventType, topicName);
        foreach (var eventData in eventDataList)
        {
            await _broker.PublishAsync(eventType, eventData, resolvedTopic, cancellationToken);
        }
    }

    internal Task DeliverAsync(
        Type eventType,
        object eventData,
        string topicName,
        CancellationToken cancellationToken)
    {
        return TriggerHandlersAsync(eventType, eventData, topicName, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _broker.Unregister(_participantId);
        return ValueTask.CompletedTask;
    }
}
