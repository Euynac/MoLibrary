using System.Collections.Concurrent;
using System.Data.Common;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.DependencyInjection.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.EfCore;
using Monica.JobScheduler.Exceptions;
using Monica.JobScheduler.Exceptions.Catalog;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Models.Operations;
using Monica.Modules;
using Monica.Repository.Persistence.Services;
using Xunit;

namespace Test.Monica.JobScheduler.Stores.EfCore;

public sealed class EfCoreJobSchedulerStoreTests
{
    private static readonly DateTimeOffset START_TIME = new(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecutionHistory_WhenEntriesExceedLimits_ShouldEvictOldestAcrossKinds()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken,
            maxExecutionHistoryEntries: 4,
            maxExecutionHistoryMessageLength: 8);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1");
        var definition = (await store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var enqueued = await EnqueueAsync(store, fixture.Scope, definition, "bounded-history");
        var capability = await store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = definition.OwnerId,
            WorkerRevisionId = definition.WorkerRevisionId,
            WorkerInstanceId = "replica-1",
            JobRevisionIds = [definition.JobRevisionId]
        }, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        var lease = (await store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = capability.LeaseKey,
            LeaseDuration = TimeSpan.FromMinutes(1),
            MaxCount = 1
        }, TestContext.Current.CancellationToken)).Single();

        foreach (var message in new[] { "first-log-entry", "second-log-entry", "third-log-entry" })
        {
            (await store.AppendExecutionLogAsync(new JobExecutionLogEntry
            {
                LeaseKey = lease.LeaseKey,
                Message = message
            }, TestContext.Current.CancellationToken)).Should().Be(JobLeaseMutationStatus.Applied);
        }

        var firstCancellation = await store.RequestCancellationAsync(
            fixture.Scope,
            enqueued.InstanceId,
            "first-cancellation-reason",
            TestContext.Current.CancellationToken);
        var repeatedCancellation = await store.RequestCancellationAsync(
            fixture.Scope,
            enqueued.InstanceId,
            "duplicate-cancellation-reason",
            TestContext.Current.CancellationToken);
        fixture.CommandInterceptor.Clear();
        var page = await store.QueryExecutionsAsync(new JobExecutionQuery
        {
            SchedulerScopeKey = fixture.Scope,
            PageSize = 10
        }, TestContext.Current.CancellationToken);
        var pageCommands = fixture.CommandInterceptor.CommandTexts;

        fixture.CommandInterceptor.Clear();
        var renewal = await store.RenewLeaseAsync(
            lease.LeaseKey,
            TimeSpan.FromMinutes(1),
            TestContext.Current.CancellationToken);
        var renewalCommands = fixture.CommandInterceptor.CommandTexts;
        var detail = await store.GetExecutionAsync(
            fixture.Scope,
            enqueued.InstanceId,
            TestContext.Current.CancellationToken);

        renewal.Status.Should().Be(JobLeaseRenewalStatus.CancellationRequested);
        pageCommands.Should().NotContain(command =>
            command.Contains("JobExecutionHistory", StringComparison.Ordinal));
        renewalCommands.Should().NotContain(command =>
            command.Contains("JobExecutionHistory", StringComparison.Ordinal));
        enqueued.History.Should().BeEmpty();
        lease.Execution.History.Should().BeEmpty();
        firstCancellation.Execution!.History.Should().BeEmpty();
        repeatedCancellation.Execution!.History.Should().BeEmpty();
        page.Items.Should().ContainSingle().Which.History.Should().BeEmpty();
        detail!.History.Should().HaveCount(4);
        detail.History.Count(item => item.Kind == JobExecutionHistoryKind.ExecutionLog).Should().Be(3);
        detail.History.Where(item => item.Kind == JobExecutionHistoryKind.ExecutionLog)
            .Select(item => item.Message)
            .Should().Equal("first-l…", "second-…", "third-l…");
        detail.History.Should().NotContain(item => item.Kind == JobExecutionHistoryKind.StateTransition);
        detail.History.Count(item => item.Kind == JobExecutionHistoryKind.Cancellation).Should().Be(1);
        detail.History.Should().AllSatisfy(item => item.Message.Length.Should().BeLessThanOrEqualTo(8));
    }

    [Fact]
    public async Task ExecutionHistory_WhenLeaseTransitionsRepeat_ShouldRetainOnlyNewestTotalEntries()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken,
            maxExecutionHistoryEntries: 3);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1");
        var definition = (await store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        await EnqueueAsync(store, fixture.Scope, definition, "bounded-transitions");
        var capability = await store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = definition.OwnerId,
            WorkerRevisionId = definition.WorkerRevisionId,
            WorkerInstanceId = "replica-1",
            JobRevisionIds = [definition.JobRevisionId]
        }, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var lease = (await store.ClaimAsync(new JobClaimRequest
            {
                CapabilityLeaseKey = capability.LeaseKey,
                LeaseDuration = TimeSpan.FromMinutes(1),
                MaxCount = 1
            }, TestContext.Current.CancellationToken)).Single();
            var released = await store.ReleaseLeaseAsync(
                lease.LeaseKey,
                TestContext.Current.CancellationToken);
            released.Status.Should().Be(JobAttemptCompletionStatus.Applied);
        }

        var detail = await store.GetExecutionAsync(
            fixture.Scope,
            "bounded-transitions",
            TestContext.Current.CancellationToken);

        detail!.State.Should().Be(JobExecutionState.Queued);
        detail.ExecutionAttempt.Should().Be(4);
        detail.History.Should().HaveCount(3);
        detail.History.Select(static entry => (entry.PreviousState, entry.NewState)).Should().Equal(
            (JobExecutionState.Running, JobExecutionState.Queued),
            (JobExecutionState.Queued, JobExecutionState.Running),
            (JobExecutionState.Running, JobExecutionState.Queued));
    }

    [Fact]
    public async Task RegisterWorkerCapability_WhenScopeContainsExpiredRows_ShouldPruneOnlyThatScope()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        var expired = await store.RegisterWorkerCapabilityAsync(
            CreateWorkerRegistration(fixture.Scope, "expired-replica"),
            TimeSpan.FromMinutes(1),
            TestContext.Current.CancellationToken);
        var live = await store.RegisterWorkerCapabilityAsync(
            CreateWorkerRegistration(fixture.Scope, "live-replica"),
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);
        var otherScopeExpired = await store.RegisterWorkerCapabilityAsync(
            CreateWorkerRegistration("other-scope", "other-expired-replica"),
            TimeSpan.FromMinutes(1),
            TestContext.Current.CancellationToken);
        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(2));

        var replacement = await store.RegisterWorkerCapabilityAsync(
            CreateWorkerRegistration(fixture.Scope, "replacement-replica"),
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        (await store.ReleaseWorkerCapabilityAsync(expired.LeaseKey, TestContext.Current.CancellationToken))
            .Should().BeFalse();
        (await store.ReleaseWorkerCapabilityAsync(live.LeaseKey, TestContext.Current.CancellationToken))
            .Should().BeTrue();
        (await store.ReleaseWorkerCapabilityAsync(otherScopeExpired.LeaseKey, TestContext.Current.CancellationToken))
            .Should().BeTrue();
        (await store.GetActiveWorkerCapabilitiesAsync(
                fixture.Scope,
                cancellationToken: TestContext.Current.CancellationToken))
            .Should().ContainSingle()
            .Which.LeaseKey.Should().Be(replacement.LeaseKey);
    }

    [Fact]
    public async Task CleanupCandidates_WhenRetentionApplies_ShouldMatchBoundedInMemorySemantics()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1");
        var active = (await store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var capability = await store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = active.OwnerId,
            WorkerRevisionId = active.WorkerRevisionId,
            WorkerInstanceId = "cleanup-replica",
            JobRevisionIds = [active.JobRevisionId]
        }, TimeSpan.FromDays(1), TestContext.Current.CancellationToken);
        for (var index = 0; index < 5; index++)
        {
            fixture.TimeProvider.Advance(TimeSpan.FromMinutes(1));
            await EnqueueAsync(store, fixture.Scope, active, $"history-{index}");
            var lease = (await store.ClaimAsync(new JobClaimRequest
            {
                CapabilityLeaseKey = capability.LeaseKey,
                LeaseDuration = TimeSpan.FromMinutes(1)
            }, TestContext.Current.CancellationToken)).Single();
            await store.CompleteAttemptAsync(new JobAttemptCompletion
            {
                LeaseKey = lease.LeaseKey,
                Outcome = JobAttemptOutcome.Succeeded
            }, TestContext.Current.CancellationToken);
        }

        IReadOnlyDictionary<string, JobHistoryRetentionPolicy> countPolicy =
            new Dictionary<string, JobHistoryRetentionPolicy>(StringComparer.Ordinal)
            {
                [active.Declaration.JobKey] = new() { MaxRecords = 2 }
            };
        var firstBatch = await store.GetExecutionCleanupCandidatesAsync(
            fixture.Scope,
            countPolicy,
            maxRetainedOrphanedExecutions: 0,
            maxDeletions: 2,
            cancellationToken: TestContext.Current.CancellationToken);
        fixture.TimeProvider.Advance(TimeSpan.FromDays(2));
        var ageBatch = await store.GetExecutionCleanupCandidatesAsync(
            fixture.Scope,
            new Dictionary<string, JobHistoryRetentionPolicy>(StringComparer.Ordinal)
            {
                [active.Declaration.JobKey] = new() { MaxDays = 1 }
            },
            maxRetainedOrphanedExecutions: 0,
            maxDeletions: 3,
            cancellationToken: TestContext.Current.CancellationToken);

        firstBatch.Should().Equal("history-0", "history-1");
        ageBatch.Should().Equal("history-0", "history-1", "history-2");

        (await store.DeleteExecutionsAsync(fixture.Scope, firstBatch, TestContext.Current.CancellationToken))
            .Should().Be(2);
        (await store.GetExecutionCleanupCandidatesAsync(
                fixture.Scope,
                countPolicy,
                maxRetainedOrphanedExecutions: 0,
                maxDeletions: 2,
                cancellationToken: TestContext.Current.CancellationToken))
            .Should().Equal("history-2");
    }

    [Fact]
    public async Task GetLatestExecutions_WhenHistoryExists_ShouldReturnOneBoundedSummaryPerDistinctKey()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1");
        var active = (await store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        await EnqueueAsync(store, fixture.Scope, active, "older");
        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        await EnqueueAsync(store, fixture.Scope, active, "newer");
        fixture.CommandInterceptor.Clear();

        var latest = await store.GetLatestExecutionsAsync(
            fixture.Scope,
            [active.Declaration.JobKey, "job-missing", active.Declaration.JobKey],
            TestContext.Current.CancellationToken);

        latest.Should().HaveCount(2);
        latest[active.Declaration.JobKey]!.InstanceId.Should().Be("newer");
        latest[active.Declaration.JobKey]!.History.Should().BeEmpty();
        latest["job-missing"].Should().BeNull();
        fixture.CommandInterceptor.CommandTexts.Should().HaveCount(2);
        fixture.CommandInterceptor.CommandTexts.Should().AllSatisfy(command =>
            command.Should().Contain("LIMIT"));
    }

    [Fact]
    public async Task Catalog_WhenReleaseIsActivated_ShouldPersistImmutableSnapshotAndPolicyEpoch()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        var manifest = CreateManifest("release-1", "revision-1");
        await store.StageReleaseAsync(new JobCatalogReleaseStage(manifest, 1), TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            CreateSnapshot("release-1", "revision-1"),
            TestContext.Current.CancellationToken);

        var activation = await store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-1",
            TestContext.Current.CancellationToken);
        var catalog = await store.GetActiveCatalogAsync(fixture.Scope, TestContext.Current.CancellationToken);
        var definition = catalog!.Definitions.Should().ContainSingle().Subject;
        var changed = await store.UpdatePolicyAsync(
            fixture.Scope,
            "worker-a",
            "job-a",
            new JobPolicyChange
            {
                DisabledOverride = true,
                MaxRetainedHistoryRecords = 20,
                ExpectedConcurrencyStamp = definition.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var version = await store.GetCatalogVersionAsync(fixture.Scope, TestContext.Current.CancellationToken);

        activation.Status.Should().Be(JobCatalogActivationStatus.Activated);
        definition.WorkerRevisionId.Should().Be("revision-1");
        changed.DisabledOverride.Should().BeTrue();
        version.ChangeEpoch.Should().Be(2);
        version.DesiredReleaseId.Should().Be("release-1");
    }

    [Fact]
    public async Task CatalogPolicy_WhenJobMovesOwners_ShouldPreservePolicyAndFenceTheFormerOwner()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1");
        var first = (await store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var firstPolicy = await store.UpdatePolicyAsync(
            fixture.Scope,
            first.OwnerId,
            first.Declaration.JobKey,
            new JobPolicyChange
            {
                DisabledOverride = true,
                MaxRetainedHistoryRecords = 20,
                ExpectedConcurrencyStamp = first.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        const string replacementOwner = "worker-b";
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2", replacementOwner), 2),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            CreateSnapshot("release-2", "revision-2", ownerId: replacementOwner),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);
        var moved = (await store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();

        moved.OwnerId.Should().Be(replacementOwner);
        moved.Policy.Should().Be(firstPolicy);
        await store.Invoking(candidate => candidate.UpdatePolicyAsync(
                fixture.Scope,
                first.OwnerId,
                first.Declaration.JobKey,
                new JobPolicyChange { ExpectedConcurrencyStamp = moved.Policy.ConcurrencyStamp },
                TestContext.Current.CancellationToken))
            .Should().ThrowAsync<JobCatalogNotFoundException>();

        var replacementPolicy = await store.UpdatePolicyAsync(
            fixture.Scope,
            replacementOwner,
            moved.Declaration.JobKey,
            new JobPolicyChange
            {
                DisabledOverride = false,
                MaxRetainedHistoryRecords = 12,
                ExpectedConcurrencyStamp = moved.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        const string emptyOwner = "worker-c";
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-3", "revision-3", emptyOwner), 3),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(fixture.Scope, "release-3", emptyOwner, "revision-3", []),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-3",
            TestContext.Current.CancellationToken);

        (await store.GetActiveCatalogAsync(fixture.Scope, TestContext.Current.CancellationToken))!
            .Definitions.Should().BeEmpty();

        await store.ReactivateReleaseAsync(
            fixture.Scope,
            "release-1",
            TestContext.Current.CancellationToken);
        var restored = (await store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();

        restored.OwnerId.Should().Be(first.OwnerId);
        restored.Policy.Should().Be(replacementPolicy);
    }

    [Fact]
    public async Task StageRelease_WhenOlderDeploymentArrivesLate_ShouldPreserveNewerDesiredIntent()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;

        var newest = await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2"), 2),
            TestContext.Current.CancellationToken);
        var delayed = await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-1", "revision-1"), 1),
            TestContext.Current.CancellationToken);
        var publication = await store.GetCatalogPublicationStatusAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken);

        newest.Status.Should().Be(ReleaseStageStatus.StagedAsDesired);
        delayed.Status.Should().Be(ReleaseStageStatus.StagedAsSuperseded);
        delayed.Version.DesiredReleaseId.Should().Be("release-2");
        delayed.Version.LastSeenDeploymentGeneration.Should().Be(2);
        delayed.Version.DesiredIntentEpoch.Should().Be(1);
        delayed.Version.PublicationEpoch.Should().Be(2);
        publication.DesiredManifestHash.Should().Be(CreateManifest("release-2", "revision-2").ContentHash);
        publication.MissingOwnerIds.Should().Equal("worker-a");

        var conflictingStage = async () => await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-conflict", "revision-3"), 2),
            TestContext.Current.CancellationToken);
        await conflictingStage.Should().ThrowAsync<JobCatalogConflictException>();
    }

    [Fact]
    public async Task StageRelease_AfterExplicitRollback_ShouldKeepRollbackIntentAndAcceptConfiguredGenerationRetry()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1");
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2"), 2),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            CreateSnapshot("release-2", "revision-2"),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);

        var rollback = await store.ReactivateReleaseAsync(
            fixture.Scope,
            "release-1",
            TestContext.Current.CancellationToken);
        var deploymentRetry = await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2"), 2),
            TestContext.Current.CancellationToken);

        rollback.ReleaseId.Should().Be("release-1");
        deploymentRetry.Status.Should().Be(ReleaseStageStatus.Duplicate);
        deploymentRetry.Version.DesiredReleaseId.Should().Be("release-1");
        deploymentRetry.Version.LastSeenDeploymentGeneration.Should().Be(2);
        deploymentRetry.Version.IsTransitionInProgress.Should().BeFalse();
    }

    [Fact]
    public async Task Enqueue_WhenIdenticalRequestIsRetriedAcrossCutover_ShouldReturnCapturedExecution()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1");
        var definition = (await store.GetActiveCatalogAsync(fixture.Scope, TestContext.Current.CancellationToken))!
            .Definitions.Single();
        var request = new JobEnqueueRequest
        {
            InstanceId = "idempotent-cutover",
            SchedulerScopeKey = fixture.Scope,
            JobKey = definition.Declaration.JobKey,
            ExpectedOwnerId = definition.OwnerId,
            ExpectedJobRevisionId = definition.JobRevisionId,
            JobArgs = "{}",
            AvailableAtUtc = START_TIME
        };
        var original = await store.EnqueueAsync(request, TestContext.Current.CancellationToken);

        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2"), 2),
            TestContext.Current.CancellationToken);
        var duringTransition = await store.EnqueueAsync(request, TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            CreateSnapshot("release-2", "revision-2"),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);
        var afterCutover = await store.EnqueueAsync(request, TestContext.Current.CancellationToken);

        duringTransition.Template.Should().Be(original.Template);
        duringTransition.State.Should().Be(JobExecutionState.Queued);
        afterCutover.Template.Should().Be(original.Template);
        afterCutover.State.Should().Be(JobExecutionState.Cancelled);
    }

    [Fact]
    public async Task Enqueue_WhenRetryOmitsAvailabilityAfterClockAdvances_ShouldReturnOriginalExecution()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        await ActivateAsync(fixture.Store, fixture.Scope, "revision-1");
        var definition = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var request = new JobEnqueueRequest
        {
            InstanceId = "authoritative-availability",
            SchedulerScopeKey = fixture.Scope,
            JobKey = definition.Declaration.JobKey,
            ExpectedOwnerId = definition.OwnerId,
            ExpectedJobRevisionId = definition.JobRevisionId,
            JobArgs = "{}"
        };

        var first = await fixture.Store.EnqueueAsync(request, TestContext.Current.CancellationToken);
        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var retry = await fixture.SecondStore.EnqueueAsync(request, TestContext.Current.CancellationToken);

        first.AvailableAtUtc.Should().Be(START_TIME);
        retry.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task Activate_WhenCuttingOver_ShouldFenceAdmissionAndRetireSupersededWork()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1");
        var definition = (await store.GetActiveCatalogAsync(fixture.Scope, TestContext.Current.CancellationToken))!
            .Definitions.Single();
        var running = await EnqueueAsync(store, fixture.Scope, definition, "a-running");
        await EnqueueAsync(store, fixture.Scope, definition, "z-queued");
        var capability = await store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = definition.OwnerId,
            WorkerRevisionId = definition.WorkerRevisionId,
            WorkerInstanceId = "replica-1",
            JobRevisionIds = [running.Template.Revision.JobRevisionId]
        }, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        await store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = capability.LeaseKey,
            LeaseDuration = TimeSpan.FromMinutes(1),
            MaxCount = 1
        }, TestContext.Current.CancellationToken);

        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2"), 2),
            TestContext.Current.CancellationToken);
        var claimsDuringTransition = await store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = capability.LeaseKey,
            LeaseDuration = TimeSpan.FromMinutes(1),
            MaxCount = 1
        }, TestContext.Current.CancellationToken);
        var queuedDuringTransition = await store.GetExecutionAsync(
            fixture.Scope,
            "z-queued",
            TestContext.Current.CancellationToken);
        var runningDuringTransition = await store.GetExecutionAsync(
            fixture.Scope,
            "a-running",
            TestContext.Current.CancellationToken);
        var enqueueDuringTransition = async () => await store.EnqueueAsync(new JobEnqueueRequest
        {
            InstanceId = "transition-admission",
            SchedulerScopeKey = fixture.Scope,
            JobKey = definition.Declaration.JobKey,
            ExpectedOwnerId = definition.OwnerId,
            ExpectedJobRevisionId = definition.JobRevisionId,
            JobArgs = "{}",
            AvailableAtUtc = START_TIME
        }, TestContext.Current.CancellationToken);

        claimsDuringTransition.Should().BeEmpty();
        queuedDuringTransition!.State.Should().Be(JobExecutionState.Queued);
        await enqueueDuringTransition.Should().ThrowAsync<JobCatalogTransitionException>();
        await store.PublishOwnerSnapshotAsync(
            CreateSnapshot("release-2", "revision-2"),
            TestContext.Current.CancellationToken);
        fixture.CommandInterceptor.Clear();
        var activation = await store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);
        var activationCommands = fixture.CommandInterceptor.CommandTexts;
        var queuedAfterCutover = await store.GetExecutionAsync(
            fixture.Scope,
            "z-queued",
            TestContext.Current.CancellationToken);
        var runningAfterCutover = await store.GetExecutionAsync(
            fixture.Scope,
            "a-running",
            TestContext.Current.CancellationToken);

        activation.Activation!.RetiredQueuedExecutionCount.Should().Be(1);
        activation.Activation.RunningCancellationRequestCount.Should().Be(1);
        queuedAfterCutover!.State.Should().Be(JobExecutionState.Cancelled);
        queuedAfterCutover.CancellationRequestedAtUtc.Should().BeNull();
        queuedAfterCutover.History.Should().BeEquivalentTo(queuedDuringTransition!.History);
        runningAfterCutover!.State.Should().Be(JobExecutionState.Running);
        runningAfterCutover.CancellationRequestedAtUtc.Should().Be(START_TIME);
        runningAfterCutover.History.Should().BeEquivalentTo(runningDuringTransition!.History);
        activationCommands.Should().NotContain(command =>
            command.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && command.Contains("\"JobExecutions\"", StringComparison.Ordinal));
        activationCommands.Count(command => command.Contains(
                "UPDATE \"JobExecutions\"",
                StringComparison.Ordinal))
            .Should().Be(2);
    }

    [Fact]
    public async Task Activate_WhenQueuedBacklogIsLarge_ShouldReportExactAggregateRetirementCount()
    {
        const int BACKLOG_SIZE = 256;
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1");
        var definition = (await store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        for (var index = 0; index < BACKLOG_SIZE; index++)
        {
            await EnqueueAsync(store, fixture.Scope, definition, $"backlog-{index:D4}");
        }

        var sampledBeforeCutover = await store.GetExecutionAsync(
            fixture.Scope,
            "backlog-0000",
            TestContext.Current.CancellationToken);
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2"), 2),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            CreateSnapshot("release-2", "revision-2"),
            TestContext.Current.CancellationToken);

        var activation = await store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);
        var statistics = await store.GetExecutionStateStatisticsAsync(
            fixture.Scope,
            cancellationToken: TestContext.Current.CancellationToken);
        var sampledAfterCutover = await store.GetExecutionAsync(
            fixture.Scope,
            "backlog-0000",
            TestContext.Current.CancellationToken);

        activation.Activation!.RetiredQueuedExecutionCount.Should().Be(BACKLOG_SIZE);
        activation.Activation.RunningCancellationRequestCount.Should().Be(0);
        statistics[JobExecutionState.Cancelled].Should().Be(BACKLOG_SIZE);
        sampledAfterCutover!.History.Should().BeEquivalentTo(sampledBeforeCutover!.History);
    }

    [Theory]
    [InlineData(JobAttemptOutcome.Succeeded, false)]
    [InlineData(JobAttemptOutcome.Cancelled, false)]
    [InlineData(JobAttemptOutcome.Succeeded, true)]
    public async Task Claim_WhenJobMovesOwners_ShouldKeepOldLeaseInsideLogicalJobGateUntilResolved(
        JobAttemptOutcome oldLeaseOutcome,
        bool expireOldLease)
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1");
        var oldDefinition = (await store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var oldExecution = await EnqueueAsync(store, fixture.Scope, oldDefinition, "old-owner-running");
        var oldCapability = await store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = oldDefinition.OwnerId,
            WorkerRevisionId = oldDefinition.WorkerRevisionId,
            WorkerInstanceId = "old-replica",
            JobRevisionIds = [oldExecution.Template.Revision.JobRevisionId]
        }, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        var oldLease = (await store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = oldCapability.LeaseKey,
            LeaseDuration = TimeSpan.FromMinutes(1),
            MaxCount = 1
        }, TestContext.Current.CancellationToken)).Single();

        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2", "worker-b"), 2),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            CreateSnapshot("release-2", "revision-2", ownerId: "worker-b"),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);
        var replacement = (await store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var replacementExecution = await EnqueueAsync(
            store,
            fixture.Scope,
            replacement,
            "new-owner-queued");
        var replacementCapability = await store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = replacement.OwnerId,
            WorkerRevisionId = replacement.WorkerRevisionId,
            WorkerInstanceId = "new-replica",
            JobRevisionIds = [replacementExecution.Template.Revision.JobRevisionId]
        }, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        var whileOldOwnerRuns = await store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = replacementCapability.LeaseKey,
            LeaseDuration = TimeSpan.FromMinutes(1),
            MaxCount = 1
        }, TestContext.Current.CancellationToken);

        whileOldOwnerRuns.Should().BeEmpty();
        (await store.GetExecutionAsync(
            fixture.Scope,
            "new-owner-queued",
            TestContext.Current.CancellationToken))!.State.Should().Be(JobExecutionState.Queued);

        if (expireOldLease)
        {
            fixture.TimeProvider.Advance(TimeSpan.FromMinutes(1));
            var recovered = await store.RecoverExpiredLeasesAsync(new ExpiredLeaseRecoveryRequest
            {
                SchedulerScopeKey = fixture.Scope,
                MaxCount = 1
            }, TestContext.Current.CancellationToken);
            recovered.Should().ContainSingle().Which.State.Should().Be(JobExecutionState.Cancelled);
        }
        else
        {
            var completion = await store.CompleteAttemptAsync(new JobAttemptCompletion
            {
                LeaseKey = oldLease.LeaseKey,
                Outcome = oldLeaseOutcome
            }, TestContext.Current.CancellationToken);
            completion.Status.Should().Be(JobAttemptCompletionStatus.Applied);
            completion.Execution!.State.Should().Be(JobExecutionState.Cancelled);
        }

        var afterOldLeaseResolved = await store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = replacementCapability.LeaseKey,
            LeaseDuration = TimeSpan.FromMinutes(1),
            MaxCount = 1
        }, TestContext.Current.CancellationToken);
        afterOldLeaseResolved.Should().ContainSingle();
        afterOldLeaseResolved[0].Execution.InstanceId.Should().Be("new-owner-queued");
        afterOldLeaseResolved[0].Execution.Template.Revision.OwnerKey.Should().Be("worker-b");
    }

    [Fact]
    public async Task SynchronizeRecurringSchedule_WhenFirstSyncIsDelayed_ShouldMaterializeFromActivationBoundary()
    {
        var activationTime = new DateTimeOffset(2026, 8, 13, 10, 0, 0, TimeSpan.Zero);
        await using var fixture = await StoreFixture.CreateAsync(
            activationTime,
            TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(
            store,
            fixture.Scope,
            "revision-1",
            JobType.Recurring,
            "0 0 * * * *");
        var active = (await store.GetActiveCatalogAsync(fixture.Scope, TestContext.Current.CancellationToken))!
            .Definitions.Single();
        fixture.TimeProvider.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(5)));
        var version = await store.GetCatalogVersionAsync(fixture.Scope, TestContext.Current.CancellationToken);

        var synchronized = await store.SynchronizeRecurringScheduleAsync(
            new RecurringScheduleSynchronization
            {
                Template = active.CreateExecutionTemplate(),
                Schedule = new RecurringScheduleDefinition
                {
                    CronExpression = "0 0 * * * *",
                    TimeZoneId = TimeZoneInfo.Utc.Id
                },
                ChangeEpoch = version.ChangeEpoch
            },
            TestContext.Current.CancellationToken);
        var due = await store.GetDueRecurringSchedulesAsync(
            fixture.Scope,
            10,
            TestContext.Current.CancellationToken);
        var cursor = due.Should().ContainSingle().Subject;
        var occurrence = activationTime.AddHours(1);
        var materialized = await store.TryMaterializeRecurringOccurrenceAsync(
            new RecurringOccurrenceMaterialization
            {
                CursorKey = cursor.Key,
                ExpectedVersion = cursor.Version,
                ExpectedOccurrenceUtc = occurrence,
                NextOccurrenceUtc = occurrence.AddHours(1),
                InstanceId = "delayed-recurring"
            },
            TestContext.Current.CancellationToken);

        synchronized.Cursor!.NextOccurrenceUtc.Should().Be(occurrence);
        materialized.Status.Should().Be(RecurringMaterializationStatus.Materialized);
        materialized.Execution!.AvailableAtUtc.Should().Be(occurrence);
    }

    [Fact]
    public async Task QueryOperationalSummaries_ShouldMatchRecurringExecutionAndWorkerState()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1", JobType.Recurring);
        var active = (await store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var version = await store.GetCatalogVersionAsync(fixture.Scope, TestContext.Current.CancellationToken);
        var synchronized = await store.SynchronizeRecurringScheduleAsync(
            new RecurringScheduleSynchronization
            {
                Template = active.CreateExecutionTemplate(),
                Schedule = new RecurringScheduleDefinition
                {
                    CronExpression = active.Declaration.CronExpression!,
                    TimeZoneId = active.Declaration.TimeZoneId!
                },
                ChangeEpoch = version.ChangeEpoch
            },
            TestContext.Current.CancellationToken);
        var occurrence = synchronized.Cursor!.NextOccurrenceUtc!.Value;
        fixture.TimeProvider.Advance(occurrence - START_TIME);
        var nextOccurrence = occurrence.AddMinutes(1);
        var materialized = await store.TryMaterializeRecurringOccurrenceAsync(
            new RecurringOccurrenceMaterialization
            {
                CursorKey = synchronized.Cursor.Key,
                ExpectedVersion = synchronized.Cursor.Version,
                ExpectedOccurrenceUtc = occurrence,
                NextOccurrenceUtc = nextOccurrence,
                InstanceId = "operational-recurring"
            },
            TestContext.Current.CancellationToken);
        await store.RegisterWorkerCapabilityAsync(
            new WorkerCapabilityRegistration
            {
                SchedulerScopeKey = fixture.Scope,
                OwnerKey = active.OwnerId,
                WorkerRevisionId = active.WorkerRevisionId,
                WorkerInstanceId = "operational-worker",
                JobRevisionIds = [active.JobRevisionId]
            },
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        var page = await store.QueryOperationalSummariesAsync(
            fixture.Scope,
            new JobCatalogQuery(),
            TestContext.Current.CancellationToken);

        var summary = page.Items.Should().ContainSingle().Subject;
        summary.RecurringScheduleStatus.Should().Be(JobRecurringScheduleStatus.Scheduled);
        summary.NextOccurrenceUtc.Should().Be(nextOccurrence);
        summary.LatestExecution!.InstanceId.Should().Be(materialized.Execution!.InstanceId);
        summary.LatestExecution.History.Should().BeEmpty();
        summary.QueuedExecutionCount.Should().Be(1);
        summary.RunningExecutionCount.Should().Be(0);
        summary.ActiveExecutionCount.Should().Be(1);
        summary.CompatibleWorkerCount.Should().Be(1);
        (await store.GetOperationalSummaryAsync(
            fixture.Scope,
            active.Declaration.JobKey,
            TestContext.Current.CancellationToken)).Should().BeEquivalentTo(summary);
        (await store.GetOperationalSummaryAsync(
            fixture.Scope,
            "job-missing",
            TestContext.Current.CancellationToken)).Should().BeNull();

        await store.UpdatePolicyAsync(
            fixture.Scope,
            active.OwnerId,
            active.Declaration.JobKey,
            new JobPolicyChange
            {
                DisabledOverride = true,
                MaxRetainedHistoryRecords = active.Policy.MaxRetainedHistoryRecords,
                MaxRetentionDays = active.Policy.MaxRetentionDays,
                ExpectedConcurrencyStamp = active.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var suspended = (await store.QueryOperationalSummariesAsync(
            fixture.Scope,
            new JobCatalogQuery(),
            TestContext.Current.CancellationToken)).Items.Single();
        suspended.RecurringScheduleStatus.Should().Be(JobRecurringScheduleStatus.Suspended);
        suspended.NextOccurrenceUtc.Should().BeNull();
    }

    [Fact]
    public async Task Activate_WhenRecurringEpochIsSuperseded_ShouldPruneItsCursor()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1", JobType.Recurring);
        var active = (await store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var version = await store.GetCatalogVersionAsync(fixture.Scope, TestContext.Current.CancellationToken);
        var synchronized = await store.SynchronizeRecurringScheduleAsync(
            new RecurringScheduleSynchronization
            {
                Template = active.CreateExecutionTemplate(),
                Schedule = new RecurringScheduleDefinition
                {
                    CronExpression = active.Declaration.CronExpression!,
                    TimeZoneId = active.Declaration.TimeZoneId!
                },
                ChangeEpoch = version.ChangeEpoch
            },
            TestContext.Current.CancellationToken);
        var obsoleteCursor = synchronized.Cursor!;
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2"), 2),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            CreateSnapshot("release-2", "revision-2", JobType.Recurring),
            TestContext.Current.CancellationToken);

        await store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);
        var materialized = await store.TryMaterializeRecurringOccurrenceAsync(
            new RecurringOccurrenceMaterialization
            {
                CursorKey = obsoleteCursor.Key,
                ExpectedVersion = obsoleteCursor.Version,
                ExpectedOccurrenceUtc = obsoleteCursor.NextOccurrenceUtc!.Value,
                NextOccurrenceUtc = obsoleteCursor.NextOccurrenceUtc.Value.AddMinutes(1),
                InstanceId = "obsolete-cursor-occurrence"
            },
            TestContext.Current.CancellationToken);

        materialized.Status.Should().Be(RecurringMaterializationStatus.CursorNotFound);
        materialized.Cursor.Should().BeNull();
    }

    [Fact]
    public async Task SynchronizeRecurringSchedule_WhenCallerClockIsSkewed_ShouldCreateAndResumeFromStoreClock()
    {
        var storeClock = START_TIME.AddSeconds(7);
        await using var fixture = await StoreFixture.CreateAsync(
            storeClock,
            TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1", JobType.Recurring);
        var active = (await store.GetActiveCatalogAsync(fixture.Scope, TestContext.Current.CancellationToken))!
            .Definitions.Single();
        var version = await store.GetCatalogVersionAsync(fixture.Scope, TestContext.Current.CancellationToken);
        var synchronization = new RecurringScheduleSynchronization
        {
            Template = active.CreateExecutionTemplate(),
            Schedule = new RecurringScheduleDefinition
            {
                CronExpression = active.Declaration.CronExpression!,
                TimeZoneId = active.Declaration.TimeZoneId!
            },
            ChangeEpoch = version.ChangeEpoch
        };

        var created = await store.SynchronizeRecurringScheduleAsync(
            synchronization,
            TestContext.Current.CancellationToken);
        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(10));
        var retentionPolicy = await store.UpdatePolicyAsync(
            fixture.Scope,
            active.OwnerId,
            active.Declaration.JobKey,
            new JobPolicyChange
            {
                MaxRetainedHistoryRecords = 50,
                ExpectedConcurrencyStamp = active.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        version = await store.GetCatalogVersionAsync(fixture.Scope, TestContext.Current.CancellationToken);
        var preserved = await store.SynchronizeRecurringScheduleAsync(
            synchronization with { ChangeEpoch = version.ChangeEpoch },
            TestContext.Current.CancellationToken);
        var disabledPolicy = await store.UpdatePolicyAsync(
            fixture.Scope,
            active.OwnerId,
            active.Declaration.JobKey,
            new JobPolicyChange
            {
                DisabledOverride = true,
                MaxRetainedHistoryRecords = 50,
                ExpectedConcurrencyStamp = retentionPolicy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        version = await store.GetCatalogVersionAsync(fixture.Scope, TestContext.Current.CancellationToken);
        var suspended = await store.SynchronizeRecurringScheduleAsync(
            synchronization with { ChangeEpoch = version.ChangeEpoch },
            TestContext.Current.CancellationToken);
        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        _ = await store.UpdatePolicyAsync(
            fixture.Scope,
            active.OwnerId,
            active.Declaration.JobKey,
            new JobPolicyChange
            {
                DisabledOverride = false,
                MaxRetainedHistoryRecords = 50,
                ExpectedConcurrencyStamp = disabledPolicy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        version = await store.GetCatalogVersionAsync(fixture.Scope, TestContext.Current.CancellationToken);
        var resumed = await store.SynchronizeRecurringScheduleAsync(
            synchronization with { ChangeEpoch = version.ChangeEpoch },
            TestContext.Current.CancellationToken);

        created.Cursor!.NextOccurrenceUtc.Should().Be(START_TIME.AddMinutes(1));
        preserved.Cursor!.NextOccurrenceUtc.Should().Be(created.Cursor.NextOccurrenceUtc);
        suspended.Cursor!.IsSuspended.Should().BeTrue();
        suspended.Cursor.NextOccurrenceUtc.Should().BeNull();
        resumed.Cursor!.IsSuspended.Should().BeFalse();
        resumed.Cursor.NextOccurrenceUtc.Should().Be(START_TIME.AddMinutes(16));
    }

    [Fact]
    public async Task MaterializeRecurring_WhenRequestIsRepeatedAcrossStores_ShouldInsertAndAdvanceOnce()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1", JobType.Recurring);
        var catalog = (await store.GetActiveCatalogAsync(fixture.Scope, TestContext.Current.CancellationToken))!;
        var definition = catalog.Definitions.Single();
        var synchronized = await store.SynchronizeRecurringScheduleAsync(new RecurringScheduleSynchronization
        {
            Template = definition.CreateExecutionTemplate(),
            Schedule = new RecurringScheduleDefinition
            {
                CronExpression = definition.Declaration.CronExpression!,
                TimeZoneId = definition.Declaration.TimeZoneId!
            },
            ChangeEpoch = catalog.Version.ChangeEpoch
        }, TestContext.Current.CancellationToken);
        var cursor = synchronized.Cursor!;
        var occurrence = cursor.NextOccurrenceUtc!.Value;
        fixture.TimeProvider.Advance(occurrence - START_TIME);
        var request = new RecurringOccurrenceMaterialization
        {
            CursorKey = cursor.Key,
            ExpectedVersion = cursor.Version,
            ExpectedOccurrenceUtc = occurrence,
            NextOccurrenceUtc = occurrence.AddMinutes(1),
            InstanceId = "recurring-once"
        };

        var first = await store.TryMaterializeRecurringOccurrenceAsync(
            request,
            TestContext.Current.CancellationToken);
        var repeated = await fixture.SecondStore.TryMaterializeRecurringOccurrenceAsync(
            request,
            TestContext.Current.CancellationToken);
        var executions = await store.QueryExecutionsAsync(new JobExecutionQuery
        {
            SchedulerScopeKey = fixture.Scope,
            PageSize = 10
        }, TestContext.Current.CancellationToken);

        first.Status.Should().Be(RecurringMaterializationStatus.Materialized);
        first.Cursor!.Version.Should().Be(cursor.Version + 1);
        first.Cursor.NextOccurrenceUtc.Should().Be(occurrence.AddMinutes(1));
        repeated.Status.Should().Be(RecurringMaterializationStatus.StaleCursor);
        repeated.Execution.Should().BeNull();
        repeated.Cursor!.Version.Should().Be(cursor.Version + 1);
        repeated.Cursor.NextOccurrenceUtc.Should().Be(occurrence.AddMinutes(1));
        executions.TotalCount.Should().Be(1);
        executions.Items.Should().ContainSingle(item => item.InstanceId == "recurring-once");
    }

    [Fact]
    public async Task GetDueRecurringSchedules_WhenOneCursorAdvances_ShouldOfferOtherOverdueCursorNext()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-1", "revision-1"), 1),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                fixture.Scope,
                "release-1",
                "worker-a",
                "revision-1",
                [CreateRecurringDeclaration("jobs.alpha"), CreateRecurringDeclaration("jobs.beta")]),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-1",
            TestContext.Current.CancellationToken);
        var catalog = (await store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!;
        foreach (var definition in catalog.Definitions)
        {
            await store.SynchronizeRecurringScheduleAsync(
                new RecurringScheduleSynchronization
                {
                    Template = definition.CreateExecutionTemplate(),
                    Schedule = new RecurringScheduleDefinition
                    {
                        CronExpression = definition.Declaration.CronExpression!,
                        TimeZoneId = definition.Declaration.TimeZoneId!
                    },
                    ChangeEpoch = catalog.Version.ChangeEpoch
                },
                TestContext.Current.CancellationToken);
        }
        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(1));

        var first = (await store.GetDueRecurringSchedulesAsync(
            fixture.Scope,
            1,
            TestContext.Current.CancellationToken)).Single();
        var occurrence = first.NextOccurrenceUtc!.Value;
        _ = await store.TryMaterializeRecurringOccurrenceAsync(
            new RecurringOccurrenceMaterialization
            {
                CursorKey = first.Key,
                ExpectedVersion = first.Version,
                ExpectedOccurrenceUtc = occurrence,
                NextOccurrenceUtc = occurrence.AddMinutes(1),
                InstanceId = "first-fair-occurrence"
            },
            TestContext.Current.CancellationToken);
        var second = (await store.GetDueRecurringSchedulesAsync(
            fixture.Scope,
            1,
            TestContext.Current.CancellationToken)).Single();

        second.Key.JobRevisionId.Should().NotBe(first.Key.JobRevisionId);
        second.NextOccurrenceUtc.Should().Be(occurrence);
    }

    [Fact]
    public async Task MaterializeRecurring_WhenPolicyWasDisabled_ShouldSuspendBeforeEvaluatingStaleCursor()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1", JobType.Recurring);
        var catalog = (await store.GetActiveCatalogAsync(fixture.Scope, TestContext.Current.CancellationToken))!;
        var definition = catalog.Definitions.Single();
        var synchronized = await store.SynchronizeRecurringScheduleAsync(new RecurringScheduleSynchronization
        {
            Template = definition.CreateExecutionTemplate(),
            Schedule = new RecurringScheduleDefinition
            {
                CronExpression = definition.Declaration.CronExpression!,
                TimeZoneId = definition.Declaration.TimeZoneId!
            },
            ChangeEpoch = catalog.Version.ChangeEpoch
        }, TestContext.Current.CancellationToken);
        await store.UpdatePolicyAsync(
            fixture.Scope,
            definition.OwnerId,
            definition.Declaration.JobKey,
            new JobPolicyChange
            {
                DisabledOverride = true,
                ExpectedConcurrencyStamp = definition.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        var materialized = await store.TryMaterializeRecurringOccurrenceAsync(
            new RecurringOccurrenceMaterialization
            {
                CursorKey = synchronized.Cursor!.Key,
                ExpectedVersion = synchronized.Cursor.Version + 10,
                ExpectedOccurrenceUtc = synchronized.Cursor.NextOccurrenceUtc!.Value,
                InstanceId = "recurring-disabled"
            },
            TestContext.Current.CancellationToken);

        materialized.Status.Should().Be(RecurringMaterializationStatus.Suspended);
        materialized.Execution.Should().BeNull();
        materialized.Cursor!.IsSuspended.Should().BeTrue();
        materialized.Cursor.NextOccurrenceUtc.Should().BeNull();
        materialized.Cursor.LastSynchronizedChangeEpoch.Should().Be(2);
    }

    [Fact]
    public async Task Enqueue_WhenDefinitionIsRecurring_ShouldRequireCursorMaterialization()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        await ActivateAsync(fixture.Store, fixture.Scope, "revision-1", JobType.Recurring);

        var enqueue = async () => await fixture.Store.EnqueueAsync(new JobEnqueueRequest
        {
            InstanceId = "recurring-through-public-api",
            SchedulerScopeKey = fixture.Scope,
            JobKey = "job-a",
            JobArgs = "{}",
            AvailableAtUtc = START_TIME
        }, TestContext.Current.CancellationToken);

        await enqueue.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Claim_WhenWorkerRevisionDiffers_ShouldLeaveExecutionQueued()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1");
        var definition = (await store.GetActiveCatalogAsync(fixture.Scope, TestContext.Current.CancellationToken))!
            .Definitions.Single();
        var execution = await store.EnqueueAsync(new JobEnqueueRequest
        {
            InstanceId = "execution-1",
            SchedulerScopeKey = fixture.Scope,
            JobKey = definition.Declaration.JobKey,
            JobArgs = "{}",
            AvailableAtUtc = START_TIME
        }, TestContext.Current.CancellationToken);
        var capability = await store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = "worker-a",
            WorkerRevisionId = "revision-2",
            WorkerInstanceId = "replica-1",
            JobRevisionIds = [execution.Template.Revision.JobRevisionId]
        }, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        var claims = await store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = capability.LeaseKey,
            LeaseDuration = TimeSpan.FromMinutes(1)
        }, TestContext.Current.CancellationToken);

        claims.Should().BeEmpty();
        (await store.GetExecutionAsync(fixture.Scope, "execution-1", TestContext.Current.CancellationToken))!
            .State.Should().Be(JobExecutionState.Queued);
    }

    [Fact]
    public async Task Claim_WhenOldestJobHasDeepSaturatedBacklog_ShouldClaimAnotherJobWithinFixedQueryBudget()
    {
        const int backlogSize = 512;
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-1", "revision-1"), 1),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                fixture.Scope,
                "release-1",
                "worker-a",
                "revision-1",
                [CreateTriggeredDeclaration("jobs.alpha"), CreateTriggeredDeclaration("jobs.beta")]),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-1",
            TestContext.Current.CancellationToken);
        var definitions = (await store.GetActiveCatalogAsync(
                fixture.Scope,
                TestContext.Current.CancellationToken))!
            .Definitions.ToDictionary(definition => definition.Declaration.JobKey, StringComparer.Ordinal);
        var alpha = definitions["jobs.alpha"];
        var beta = definitions["jobs.beta"];
        await EnqueueAsync(store, fixture.Scope, alpha, "alpha-running");
        var capability = await store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = "worker-a",
            WorkerRevisionId = "revision-1",
            WorkerInstanceId = "replica-1",
            JobRevisionIds = [alpha.JobRevisionId, beta.JobRevisionId]
        }, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        _ = await store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = capability.LeaseKey,
            LeaseDuration = TimeSpan.FromMinutes(1),
            MaxCount = 1
        }, TestContext.Current.CancellationToken);

        for (var index = 0; index < backlogSize; index++)
        {
            await EnqueueAsync(store, fixture.Scope, alpha, $"alpha-{index:D4}");
        }
        await EnqueueAsync(store, fixture.Scope, beta, "z-beta-runnable");
        fixture.CommandInterceptor.Clear();

        var claims = await store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = capability.LeaseKey,
            LeaseDuration = TimeSpan.FromMinutes(1),
            MaxCount = 1
        }, TestContext.Current.CancellationToken);
        var selectCommands = fixture.CommandInterceptor.CommandTexts
            .Where(command => command.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        claims.Should().ContainSingle();
        claims[0].Execution.InstanceId.Should().Be("z-beta-runnable");
        claims[0].Execution.Template.Revision.JobKey.Should().Be("jobs.beta");
        selectCommands.Should().HaveCountLessThanOrEqualTo(5);
        selectCommands.Count(command => command.Contains("\"JobExecutions\"", StringComparison.Ordinal))
            .Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task CompleteAttempt_WhenLeaseWasFenced_ShouldRejectStaleCompletion()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1");
        var definition = (await store.GetActiveCatalogAsync(fixture.Scope, TestContext.Current.CancellationToken))!
            .Definitions.Single();
        var execution = await store.EnqueueAsync(new JobEnqueueRequest
        {
            InstanceId = "execution-1",
            SchedulerScopeKey = fixture.Scope,
            JobKey = definition.Declaration.JobKey,
            JobArgs = "{}",
            AvailableAtUtc = START_TIME
        }, TestContext.Current.CancellationToken);
        var registration = new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = "worker-a",
            WorkerRevisionId = "revision-1",
            WorkerInstanceId = "replica-1",
            JobRevisionIds = [execution.Template.Revision.JobRevisionId]
        };
        var capability = await store.RegisterWorkerCapabilityAsync(
            registration,
            TimeSpan.FromMinutes(1),
            TestContext.Current.CancellationToken);
        var lease = (await store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = capability.LeaseKey,
            LeaseDuration = TimeSpan.FromMinutes(1)
        }, TestContext.Current.CancellationToken)).Single();
        await store.RegisterWorkerCapabilityAsync(registration, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        var completion = await store.CompleteAttemptAsync(new JobAttemptCompletion
        {
            LeaseKey = lease.LeaseKey,
            Outcome = JobAttemptOutcome.Succeeded
        }, TestContext.Current.CancellationToken);

        completion.Status.Should().Be(JobAttemptCompletionStatus.Lost);
    }

    [Fact]
    public async Task Claim_WhenTwoStoresContend_ShouldLeaseExecutionOnlyOnce()
    {
        await using var fixture = await StoreFixture.CreateAsync(START_TIME, TestContext.Current.CancellationToken);
        var store = fixture.Store;
        await ActivateAsync(store, fixture.Scope, "revision-1");
        var definition = (await store.GetActiveCatalogAsync(fixture.Scope, TestContext.Current.CancellationToken))!
            .Definitions.Single();
        var execution = await store.EnqueueAsync(new JobEnqueueRequest
        {
            InstanceId = "execution-1",
            SchedulerScopeKey = fixture.Scope,
            JobKey = definition.Declaration.JobKey,
            JobArgs = "{}",
            AvailableAtUtc = START_TIME
        }, TestContext.Current.CancellationToken);
        var firstCapability = await store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = "worker-a",
            WorkerRevisionId = "revision-1",
            WorkerInstanceId = "replica-1",
            JobRevisionIds = [execution.Template.Revision.JobRevisionId]
        }, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        var secondCapability = await store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = "worker-a",
            WorkerRevisionId = "revision-1",
            WorkerInstanceId = "replica-2",
            JobRevisionIds = [execution.Template.Revision.JobRevisionId]
        }, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        var attempts = await Task.WhenAll(
            store.ClaimAsync(new JobClaimRequest
            {
                CapabilityLeaseKey = firstCapability.LeaseKey,
                LeaseDuration = TimeSpan.FromMinutes(1)
            }, TestContext.Current.CancellationToken),
            fixture.SecondStore.ClaimAsync(new JobClaimRequest
            {
                CapabilityLeaseKey = secondCapability.LeaseKey,
                LeaseDuration = TimeSpan.FromMinutes(1)
            }, TestContext.Current.CancellationToken));

        attempts.Sum(item => item.Count).Should().Be(1);
        (await store.GetExecutionAsync(fixture.Scope, "execution-1", TestContext.Current.CancellationToken))!
            .State.Should().Be(JobExecutionState.Running);
    }

    private static async Task ActivateAsync(
        IJobSchedulerStore store,
        string scope,
        string revision,
        JobType jobType = JobType.Triggered,
        string? cronExpression = null)
    {
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-1", revision), 1),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            CreateSnapshot("release-1", revision, jobType, cronExpression: cronExpression),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(scope, "release-1", TestContext.Current.CancellationToken);
    }

    private static Task<JobExecutionInstance> EnqueueAsync(
        IJobSchedulerStore store,
        string scope,
        ActiveJobDefinition definition,
        string instanceId) => store.EnqueueAsync(new JobEnqueueRequest
    {
        InstanceId = instanceId,
        SchedulerScopeKey = scope,
        JobKey = definition.Declaration.JobKey,
        ExpectedOwnerId = definition.OwnerId,
        ExpectedJobRevisionId = definition.JobRevisionId,
        JobArgs = "{}",
        AvailableAtUtc = START_TIME
    }, TestContext.Current.CancellationToken);

    private static WorkerCapabilityRegistration CreateWorkerRegistration(
        string schedulerScopeKey,
        string workerInstanceId) => new()
    {
        SchedulerScopeKey = schedulerScopeKey,
        OwnerKey = "worker-a",
        WorkerRevisionId = "revision-1",
        WorkerInstanceId = workerInstanceId
    };

    private static JobCatalogReleaseManifest CreateManifest(
        string release,
        string revision,
        string ownerId = "worker-a") =>
        new("ef-store-tests", release, [new JobCatalogOwnerManifest(ownerId, revision)]);

    private static JobOwnerCatalogSnapshot CreateSnapshot(
        string release,
        string revision,
        JobType jobType = JobType.Triggered,
        string ownerId = "worker-a",
        string? cronExpression = null) =>
        new("ef-store-tests", release, ownerId, revision,
        [
            new JobDeclaration
            {
                JobKey = "job-a",
                JobName = "Job A",
                JobType = jobType,
                JobArgsKey = jobType == JobType.Triggered ? "JobAArgs" : null,
                CronExpression = jobType == JobType.Recurring ? cronExpression ?? "0 * * * * *" : null,
                TimeZoneId = jobType == JobType.Recurring ? "UTC" : null,
                MaxExecutionTimeout = TimeSpan.FromMinutes(1)
            }
        ]);

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

    private static JobDeclaration CreateTriggeredDeclaration(string jobKey) => new()
    {
        JobKey = jobKey,
        JobArgsKey = $"{jobKey}.Args",
        JobName = jobKey,
        JobType = JobType.Triggered,
        MaxConcurrency = 1,
        MaxExecutionTimeout = TimeSpan.FromMinutes(1)
    };

    private sealed class StoreFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly SqliteConnection _connection;

        private StoreFixture(
            ServiceProvider serviceProvider,
            SqliteConnection connection,
            ManualTimeProvider timeProvider,
            RecordingCommandInterceptor commandInterceptor,
            IOptions<ModuleJobSchedulerOption> schedulerOptions)
        {
            _serviceProvider = serviceProvider;
            _connection = connection;
            var factory = new SqliteStoreDbContextFactory(
                connection.ConnectionString,
                serviceProvider,
                commandInterceptor);
            Store = new EfCoreJobSchedulerStore(
                factory,
                timeProvider,
                schedulerOptions);
            SecondStore = new EfCoreJobSchedulerStore(
                factory,
                timeProvider,
                schedulerOptions);
            TimeProvider = timeProvider;
            CommandInterceptor = commandInterceptor;
        }

        internal string Scope => "ef-store-tests";
        internal EfCoreJobSchedulerStore Store { get; }
        internal EfCoreJobSchedulerStore SecondStore { get; }
        internal ManualTimeProvider TimeProvider { get; }
        internal RecordingCommandInterceptor CommandInterceptor { get; }

        internal static async Task<StoreFixture> CreateAsync(
            DateTimeOffset now,
            CancellationToken cancellationToken,
            int maxExecutionHistoryEntries = 1000,
            int maxExecutionHistoryMessageLength = 4096)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions();
            services.AddSingleton<IOptions<ModuleRepositoryOption>>(Options.Create(new ModuleRepositoryOption()));
            services.AddSingleton<ICachedServiceProvider, global::Monica.DependencyInjection.Services.CachedServiceProvider>();
            var connection = new SqliteConnection(
                $"Data Source=ef-store-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await connection.OpenAsync(cancellationToken);
            var provider = services.BuildServiceProvider();
            var commandInterceptor = new RecordingCommandInterceptor();
            var factory = new SqliteStoreDbContextFactory(
                connection.ConnectionString,
                provider,
                commandInterceptor);
            await using (var context = await factory.CreateDbContextAsync(cancellationToken))
            {
                await context.Database.EnsureCreatedAsync(cancellationToken);
            }

            return new StoreFixture(
                provider,
                connection,
                new ManualTimeProvider(now),
                commandInterceptor,
                Options.Create(new ModuleJobSchedulerOption
                {
                    MaxExecutionHistoryEntriesPerExecution = maxExecutionHistoryEntries,
                    MaxExecutionHistoryMessageLength = maxExecutionHistoryMessageLength
                }));
        }

        public async ValueTask DisposeAsync()
        {
            await _serviceProvider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private sealed class SqliteStoreDbContextFactory(
        string connectionString,
        IServiceProvider serviceProvider,
        RecordingCommandInterceptor commandInterceptor) : IDbContextFactory<JobSchedulerDbContext>
    {
        public JobSchedulerDbContext CreateDbContext()
        {
            return new JobSchedulerDbContext(
                new DbContextOptionsBuilder<JobSchedulerDbContext>()
                    .UseSqlite(connectionString)
                    .AddInterceptors(commandInterceptor)
                    .Options,
                serviceProvider.GetRequiredService<ICachedServiceProvider>());
        }

        public ValueTask<JobSchedulerDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(CreateDbContext());
        }
    }

    private sealed class RecordingCommandInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<string> _commandTexts = new();

        internal IReadOnlyCollection<string> CommandTexts => _commandTexts.ToArray();

        internal void Clear()
        {
            while (_commandTexts.TryDequeue(out _))
            {
            }
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            _commandTexts.Enqueue(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _commandTexts.Enqueue(command.CommandText);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
