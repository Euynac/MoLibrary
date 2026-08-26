using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Models;
using Monica.Core.ObservableInstance.Services;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Operations;
using Monica.JobScheduler.Models.Definitions;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Providers;
using Monica.JobScheduler.Services.Support;
using Monica.Modules;
using Test.Monica.JobScheduler.Stores.Shared;
using Xunit;

namespace Test.Monica.JobScheduler.Services;

public sealed class JobSchedulingHostedServiceTests
{
    private static readonly DateTimeOffset NOW = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ScheduleAsync_ShouldSyncSnapshotMaterializeDueOccurrencesAndCoalesceOutages()
    {
        var time = new StoreFixture.ManualTimeProvider(NOW);
        var store = new InMemoryJobSchedulerStore(time);
        var definitions = new[]
        {
            new LocalJobDefinition
            {
                Declaration = StoreFixture.RecurringDeclaration("jobs.alpha", cron: "0 0 * * * *"),
                JobClrType = typeof(object)
            }
        };
        using var service = CreateService(store, definitions, time);

        await service.ScheduleAsync(TestContext.Current.CancellationToken);
        var operational = await store.GetOperationalSummaryAsync(
            StoreFixture.SCOPE,
            new JobId(StoreFixture.OWNER_A, "jobs.alpha"), TestContext.Current.CancellationToken);
        operational!.RecurringScheduleStatus.Should().Be(JobRecurringScheduleStatus.Scheduled);
        operational.NextOccurrenceUtc.Should().Be(NOW.AddHours(1));

        // A long outage coalesces missed occurrences into one execution and advances to the first future occurrence.
        time.Advance(TimeSpan.FromHours(5));
        await service.ScheduleAsync(TestContext.Current.CancellationToken);
        var page = await store.QueryExecutionsAsync(new JobExecutionQuery
        {
            SchedulerScopeKey = StoreFixture.SCOPE,
            JobKey = "jobs.alpha"
        }, TestContext.Current.CancellationToken);
        page.TotalCount.Should().Be(1);
        var materialized = page.Items.Single();
        materialized.Origin.Should().Be(JobExecutionOrigin.RecurringSchedule);
        materialized.RecurringOccurrenceUtc.Should().Be(NOW.AddHours(1));

        var afterOutage = await store.GetOperationalSummaryAsync(
            StoreFixture.SCOPE,
            new JobId(StoreFixture.OWNER_A, "jobs.alpha"), TestContext.Current.CancellationToken);
        afterOutage!.NextOccurrenceUtc.Should().Be(NOW.AddHours(6));
    }

    [Fact]
    public async Task ScheduleAsync_InDebugMode_ShouldSuppressAutomaticMaterialization()
    {
        var time = new StoreFixture.ManualTimeProvider(NOW);
        var store = new InMemoryJobSchedulerStore(time);
        var definitions = new[]
        {
            new LocalJobDefinition
            {
                Declaration = StoreFixture.RecurringDeclaration("jobs.alpha", cron: "0 0 * * * *"),
                JobClrType = typeof(object)
            }
        };
        using var service = CreateService(store, definitions, time, debugMode: true);
        await service.ScheduleAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromHours(2));
        await service.ScheduleAsync(TestContext.Current.CancellationToken);

        var operational = await store.GetOperationalSummaryAsync(
            StoreFixture.SCOPE,
            new JobId(StoreFixture.OWNER_A, "jobs.alpha"), TestContext.Current.CancellationToken);
        operational!.IsSuspended.Should().BeTrue();
        operational.RecurringScheduleStatus.Should().Be(JobRecurringScheduleStatus.Suspended);
        var page = await store.QueryExecutionsAsync(new JobExecutionQuery
        {
            SchedulerScopeKey = StoreFixture.SCOPE,
            JobKey = "jobs.alpha"
        }, TestContext.Current.CancellationToken);
        page.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task ScheduleAsync_WithExpiredLease_ShouldRecoverAcrossOwners()
    {
        var time = new StoreFixture.ManualTimeProvider(NOW);
        var store = new InMemoryJobSchedulerStore(time);
        await store.SyncOwnerSnapshotAsync(new JobOwnerSnapshot(
            StoreFixture.SCOPE,
            StoreFixture.OWNER_B,
            [StoreFixture.TriggeredDeclaration("jobs.beta")]), TestContext.Current.CancellationToken);
        await store.EnqueueAsync(new JobEnqueueRequest
        {
            SchedulerScopeKey = StoreFixture.SCOPE,
            OwnerKey = StoreFixture.OWNER_B,
            JobKey = "jobs.beta",
            InstanceId = "other-owner-exec",
            JobArgs = "{\"v\":1}"
        }, TestContext.Current.CancellationToken);
        var lease = (await store.ClaimAsync(new JobClaimRequest
        {
            SchedulerScopeKey = StoreFixture.SCOPE,
            OwnerKey = StoreFixture.OWNER_B,
            WorkerInstanceId = "worker-b",
            JobKeys = ["jobs.beta"],
            LeaseDuration = TimeSpan.FromSeconds(30)
        }, TestContext.Current.CancellationToken)).Single();

        time.Advance(TimeSpan.FromSeconds(31));
        // The scheduling host of owner A recovers the expired lease of owner B.
        using var service = CreateService(store, [], time);
        await service.ScheduleAsync(TestContext.Current.CancellationToken);

        var recovered = await store.GetExecutionAsync(StoreFixture.SCOPE, "other-owner-exec", TestContext.Current.CancellationToken);
        recovered!.State.Should().Be(JobExecutionState.Queued);
        (await store.RenewLeaseAsync(lease.LeaseKey, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)).Status
            .Should().Be(JobLeaseRenewalStatus.Lost);
    }

    private static JobSchedulingHostedService CreateService(
        InMemoryJobSchedulerStore store,
        IReadOnlyList<LocalJobDefinition> definitions,
        StoreFixture.ManualTimeProvider time,
        bool debugMode = false)
    {
        var options = Options.Create(new ModuleJobSchedulerOption
        {
            SchedulerScopeKey = StoreFixture.SCOPE,
            ProjectName = StoreFixture.OWNER_A,
            RecurringJobDebugMode = debugMode,
            EnableHistoryCleanup = false
        });
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        return new JobSchedulingHostedService(
            store,
            definitions,
            new JobSchedulerRuntimeState(),
            time,
            options,
            new ObservableInstanceRegistry(Options.Create(new ModuleObservableInstanceOption())),
            Options.Create(new ModuleHostedServiceOption()),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobSchedulingHostedService>.Instance);
    }
}
