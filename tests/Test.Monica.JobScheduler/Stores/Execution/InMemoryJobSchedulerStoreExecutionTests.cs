using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Models.Operations;
using Monica.JobScheduler.Providers;
using Monica.Modules;
using Xunit;

namespace Test.Monica.JobScheduler.Stores.Execution;

public sealed partial class InMemoryJobSchedulerStoreExecutionTests
{
    private static readonly DateTimeOffset START_TIME = new(2026, 8, 13, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public async Task ExecutionHistory_WhenEntriesExceedLimits_ShouldEvictOldestAcrossKinds()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time, Options.Create(new ModuleJobSchedulerOption
        {
            MaxExecutionHistoryEntriesPerExecution = 4,
            MaxExecutionHistoryMessageLength = 8
        }));
        var active = await ActivateTriggeredDefinitionAsync(
            store,
            cancellationToken: TestContext.Current.CancellationToken);
        var template = active.CreateExecutionTemplate();
        var enqueued = await store.EnqueueAsync(
            CreateEnqueue("bounded-history", template, START_TIME),
            TestContext.Current.CancellationToken);
        var capability = await RegisterWorkerAsync(
            store,
            "worker-1",
            template.Revision.WorkerRevisionId,
            [template.Revision.JobRevisionId],
            cancellationToken: TestContext.Current.CancellationToken);
        var lease = await ClaimOneAsync(store, capability, TestContext.Current.CancellationToken);

        foreach (var message in new[] { "first-log-entry", "second-log-entry", "third-log-entry" })
        {
            (await store.AppendExecutionLogAsync(new JobExecutionLogEntry
            {
                LeaseKey = lease.LeaseKey,
                Message = message
            }, TestContext.Current.CancellationToken)).Should().Be(JobLeaseMutationStatus.Applied);
        }

        var firstCancellation = await store.RequestCancellationAsync(
            SCOPE,
            enqueued.InstanceId,
            "first-cancellation-reason",
            TestContext.Current.CancellationToken);
        var repeatedCancellation = await store.RequestCancellationAsync(
            SCOPE,
            enqueued.InstanceId,
            "duplicate-cancellation-reason",
            TestContext.Current.CancellationToken);
        var page = await store.QueryExecutionsAsync(new JobExecutionQuery
        {
            SchedulerScopeKey = SCOPE,
            PageSize = 10
        }, TestContext.Current.CancellationToken);
        var detail = await store.GetExecutionAsync(
            SCOPE,
            enqueued.InstanceId,
            TestContext.Current.CancellationToken);

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
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time, Options.Create(new ModuleJobSchedulerOption
        {
            MaxExecutionHistoryEntriesPerExecution = 3
        }));
        var active = await ActivateTriggeredDefinitionAsync(
            store,
            cancellationToken: TestContext.Current.CancellationToken);
        var template = active.CreateExecutionTemplate();
        await store.EnqueueAsync(
            CreateEnqueue("bounded-transitions", template, START_TIME),
            TestContext.Current.CancellationToken);
        var capability = await RegisterWorkerAsync(
            store,
            "worker-1",
            template.Revision.WorkerRevisionId,
            [template.Revision.JobRevisionId],
            cancellationToken: TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var lease = await ClaimOneAsync(store, capability, TestContext.Current.CancellationToken);
            var released = await store.ReleaseLeaseAsync(
                lease.LeaseKey,
                TestContext.Current.CancellationToken);
            released.Status.Should().Be(JobAttemptCompletionStatus.Applied);
        }

        var detail = await store.GetExecutionAsync(
            SCOPE,
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
    public async Task RegisterWorkerCapabilityAsync_WhenScopeContainsExpiredRows_ShouldPruneOnlyThatScope()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var expired = await store.RegisterWorkerCapabilityAsync(
            CreateWorkerRegistration(SCOPE, "expired-replica"),
            TimeSpan.FromMinutes(1),
            TestContext.Current.CancellationToken);
        var live = await store.RegisterWorkerCapabilityAsync(
            CreateWorkerRegistration(SCOPE, "live-replica"),
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);
        var otherScopeExpired = await store.RegisterWorkerCapabilityAsync(
            CreateWorkerRegistration("other-scope", "other-expired-replica"),
            TimeSpan.FromMinutes(1),
            TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(2));

        var replacement = await store.RegisterWorkerCapabilityAsync(
            CreateWorkerRegistration(SCOPE, "replacement-replica"),
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        (await store.ReleaseWorkerCapabilityAsync(expired.LeaseKey, TestContext.Current.CancellationToken))
            .Should().BeFalse();
        (await store.ReleaseWorkerCapabilityAsync(live.LeaseKey, TestContext.Current.CancellationToken))
            .Should().BeTrue();
        (await store.ReleaseWorkerCapabilityAsync(otherScopeExpired.LeaseKey, TestContext.Current.CancellationToken))
            .Should().BeTrue();
        (await store.GetActiveWorkerCapabilitiesAsync(SCOPE, cancellationToken: TestContext.Current.CancellationToken))
            .Should().ContainSingle()
            .Which.LeaseKey.Should().Be(replacement.LeaseKey);
    }

    [Fact]
    public async Task CleanupCandidates_WhenRetentionApplies_ShouldReturnOldestGloballyBoundedBatches()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var active = await ActivateTriggeredDefinitionAsync(
            store,
            cancellationToken: TestContext.Current.CancellationToken);
        var capability = await RegisterWorkerAsync(
            store,
            "cleanup-replica",
            active.WorkerRevisionId,
            [active.JobRevisionId],
            TimeSpan.FromDays(1),
            TestContext.Current.CancellationToken);
        for (var index = 0; index < 5; index++)
        {
            time.Advance(TimeSpan.FromMinutes(1));
            await store.EnqueueAsync(
                CreateEnqueue($"history-{index}", active.CreateExecutionTemplate(), time.GetUtcNow()),
                TestContext.Current.CancellationToken);
            var lease = await ClaimOneAsync(store, capability, TestContext.Current.CancellationToken);
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
            SCOPE,
            countPolicy,
            maxRetainedOrphanedExecutions: 0,
            maxDeletions: 2,
            cancellationToken: TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromDays(2));
        var ageBatch = await store.GetExecutionCleanupCandidatesAsync(
            SCOPE,
            new Dictionary<string, JobHistoryRetentionPolicy>(StringComparer.Ordinal)
            {
                [active.Declaration.JobKey] = new() { MaxDays = 1 }
            },
            maxRetainedOrphanedExecutions: 0,
            maxDeletions: 3,
            cancellationToken: TestContext.Current.CancellationToken);

        firstBatch.Should().Equal("history-0", "history-1");
        ageBatch.Should().Equal("history-0", "history-1", "history-2");

        (await store.DeleteExecutionsAsync(SCOPE, firstBatch, TestContext.Current.CancellationToken))
            .Should().Be(2);
        (await store.GetExecutionCleanupCandidatesAsync(
                SCOPE,
                countPolicy,
                maxRetainedOrphanedExecutions: 0,
                maxDeletions: 2,
                cancellationToken: TestContext.Current.CancellationToken))
            .Should().Equal("history-2");
    }

    [Fact]
    public async Task GetLatestExecutionsAsync_WhenHistoryExists_ShouldReturnOneSummaryPerDistinctKey()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var active = await ActivateTriggeredDefinitionAsync(
            store,
            cancellationToken: TestContext.Current.CancellationToken);
        var template = active.CreateExecutionTemplate();
        await store.EnqueueAsync(
            CreateEnqueue("older", template, time.GetUtcNow()),
            TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(1));
        await store.EnqueueAsync(
            CreateEnqueue("newer", template, time.GetUtcNow()),
            TestContext.Current.CancellationToken);

        var latest = await store.GetLatestExecutionsAsync(
            SCOPE,
            [active.Declaration.JobKey, "jobs.missing", active.Declaration.JobKey],
            TestContext.Current.CancellationToken);

        latest.Should().HaveCount(2);
        latest[active.Declaration.JobKey]!.InstanceId.Should().Be("newer");
        latest[active.Declaration.JobKey]!.History.Should().BeEmpty();
        latest["jobs.missing"].Should().BeNull();
    }

    [Fact]
    public async Task ClaimAsync_WhenRevisionsDiffer_ShouldOnlyClaimExactWorkerCapability()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var active = await ActivateTriggeredDefinitionAsync(
            store,
            workerRevisionId: "worker-v2",
            cancellationToken: TestContext.Current.CancellationToken);
        var template = active.CreateExecutionTemplate();
        await store.EnqueueAsync(
            CreateEnqueue("execution-1", template, START_TIME),
            TestContext.Current.CancellationToken);
        var incompatible = await RegisterWorkerAsync(
            store,
            "worker-1",
            "worker-v1",
            [template.Revision.JobRevisionId],
            cancellationToken: TestContext.Current.CancellationToken);

        var incompatibleClaims = await store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = incompatible.LeaseKey,
            MaxCount = 1,
            LeaseDuration = TimeSpan.FromMinutes(1)
        }, TestContext.Current.CancellationToken);

        incompatibleClaims.Should().BeEmpty();

        var compatible = await RegisterWorkerAsync(
            store,
            "worker-2",
            "worker-v2",
            [template.Revision.JobRevisionId],
            cancellationToken: TestContext.Current.CancellationToken);
        var compatibleClaims = await store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = compatible.LeaseKey,
            MaxCount = 1,
            LeaseDuration = TimeSpan.FromMinutes(1)
        }, TestContext.Current.CancellationToken);

        compatibleClaims.Should().ContainSingle();
        compatibleClaims[0].Execution.State.Should().Be(JobExecutionState.Running);
        compatibleClaims[0].Execution.Template.Revision.WorkerRevisionId.Should().Be("worker-v2");
        compatibleClaims[0].Execution.Template.Revision.JobRevisionId.Should().Be(template.Revision.JobRevisionId);
    }

    [Fact]
    public async Task ClaimAsync_WhenOldestJobHasDeepSaturatedBacklog_ShouldClaimAnotherLogicalJob()
    {
        const int backlogSize = 512;
        var store = new InMemoryJobSchedulerStore(new ManualTimeProvider(START_TIME));
        var owner = new JobCatalogOwnerManifest("owner-a", "worker-v1");
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(
                new JobCatalogReleaseManifest(SCOPE, "release-v1", [owner]),
                1),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(new JobOwnerCatalogSnapshot(
            SCOPE,
            "release-v1",
            owner.OwnerId,
            owner.WorkerRevisionId,
            [CreateTriggeredDeclaration("jobs.alpha"), CreateTriggeredDeclaration("jobs.beta")]),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(SCOPE, "release-v1", TestContext.Current.CancellationToken);
        var definitions = (await store.GetActiveCatalogAsync(SCOPE, TestContext.Current.CancellationToken))!
            .Definitions.ToDictionary(definition => definition.Declaration.JobKey, StringComparer.Ordinal);
        var alpha = definitions["jobs.alpha"];
        var beta = definitions["jobs.beta"];
        await store.EnqueueAsync(
            CreateEnqueue("alpha-running", alpha.CreateExecutionTemplate(), START_TIME),
            TestContext.Current.CancellationToken);
        var capability = await RegisterWorkerAsync(
            store,
            "worker-1",
            owner.WorkerRevisionId,
            [alpha.JobRevisionId, beta.JobRevisionId],
            cancellationToken: TestContext.Current.CancellationToken);
        _ = await ClaimOneAsync(store, capability, TestContext.Current.CancellationToken);

        for (var index = 0; index < backlogSize; index++)
        {
            await store.EnqueueAsync(
                CreateEnqueue($"alpha-{index:D4}", alpha.CreateExecutionTemplate(), START_TIME),
                TestContext.Current.CancellationToken);
        }
        await store.EnqueueAsync(
            CreateEnqueue("z-beta-runnable", beta.CreateExecutionTemplate(), START_TIME),
            TestContext.Current.CancellationToken);

        var claims = await ClaimAllAsync(store, capability, 1, TestContext.Current.CancellationToken);

        claims.Should().ContainSingle();
        claims[0].Execution.InstanceId.Should().Be("z-beta-runnable");
        claims[0].Execution.Template.Revision.JobKey.Should().Be("jobs.beta");
    }

    [Fact]
    public async Task ClaimAsync_WhenRequestedCountExceedsBound_ShouldRejectBeforeResolvingCapability()
    {
        var store = new InMemoryJobSchedulerStore(new ManualTimeProvider(START_TIME));

        var claim = async () => await store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = new WorkerCapabilityLeaseKey
            {
                SchedulerScopeKey = SCOPE,
                WorkerInstanceId = "worker-1",
                LeaseToken = "lease-token"
            },
            MaxCount = JobClaimRequest.MAX_COUNT + 1,
            LeaseDuration = TimeSpan.FromMinutes(1)
        }, TestContext.Current.CancellationToken);

        await claim.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(JobClaimRequest.MaxCount));
    }

    [Fact]
    public async Task ClaimAsync_WhenLogicalJobGateIsFull_ShouldWaitWithoutSkippingWork()
    {
        var store = new InMemoryJobSchedulerStore(new ManualTimeProvider(START_TIME));
        var active = await ActivateTriggeredDefinitionAsync(
            store,
            maxConcurrency: 1,
            cancellationToken: TestContext.Current.CancellationToken);
        var template = active.CreateExecutionTemplate();
        await store.EnqueueAsync(
            CreateEnqueue("execution-1", template, START_TIME),
            TestContext.Current.CancellationToken);
        await store.EnqueueAsync(
            CreateEnqueue("execution-2", template, START_TIME),
            TestContext.Current.CancellationToken);
        var capability = await RegisterWorkerAsync(store, "worker-1", template.Revision.WorkerRevisionId,
            [template.Revision.JobRevisionId], cancellationToken: TestContext.Current.CancellationToken);

        var first = await ClaimOneAsync(store, capability, TestContext.Current.CancellationToken);
        var whileFull = await ClaimAllAsync(store, capability, 1, TestContext.Current.CancellationToken);

        whileFull.Should().BeEmpty();
        (await store.GetExecutionAsync(SCOPE, "execution-2", TestContext.Current.CancellationToken))!
            .State.Should().Be(JobExecutionState.Queued);

        var completion = await store.CompleteAttemptAsync(new JobAttemptCompletion
        {
            LeaseKey = first.LeaseKey,
            Outcome = JobAttemptOutcome.Succeeded
        }, TestContext.Current.CancellationToken);
        var second = await ClaimAllAsync(store, capability, 1, TestContext.Current.CancellationToken);

        completion.Status.Should().Be(JobAttemptCompletionStatus.Applied);
        second.Should().ContainSingle();
        second[0].Execution.InstanceId.Should().Be("execution-2");
    }

    [Theory]
    [InlineData(JobAttemptOutcome.Succeeded, false)]
    [InlineData(JobAttemptOutcome.Cancelled, false)]
    [InlineData(JobAttemptOutcome.Succeeded, true)]
    public async Task ClaimAsync_WhenJobMovesOwners_ShouldKeepOldLeaseInsideLogicalJobGateUntilResolved(
        JobAttemptOutcome oldLeaseOutcome,
        bool expireOldLease)
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var oldDefinition = await ActivateTriggeredDefinitionAsync(
            store,
            maxConcurrency: 1,
            cancellationToken: TestContext.Current.CancellationToken);
        var oldTemplate = oldDefinition.CreateExecutionTemplate();
        await store.EnqueueAsync(
            CreateEnqueue("old-owner-running", oldTemplate, START_TIME),
            TestContext.Current.CancellationToken);
        var oldCapability = await RegisterWorkerAsync(
            store,
            "old-replica",
            oldTemplate.Revision.WorkerRevisionId,
            [oldTemplate.Revision.JobRevisionId],
            cancellationToken: TestContext.Current.CancellationToken);
        var oldLease = await ClaimOneAsync(
            store,
            oldCapability,
            TestContext.Current.CancellationToken,
            TimeSpan.FromMinutes(1));

        var replacementOwner = new JobCatalogOwnerManifest("owner-b", "worker-v2");
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(
                new JobCatalogReleaseManifest(SCOPE, "release-v2", [replacementOwner]),
                2),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(new JobOwnerCatalogSnapshot(
            SCOPE,
            "release-v2",
            replacementOwner.OwnerId,
            replacementOwner.WorkerRevisionId,
            [
                new JobDeclaration
                {
                    JobKey = oldTemplate.Revision.JobKey,
                    JobArgsKey = "Jobs.AlphaArgs",
                    JobName = "Alpha",
                    JobType = JobType.Triggered,
                    MaxConcurrency = 1,
                    MaxExecutionTimeout = TimeSpan.FromMinutes(5)
                }
            ]),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(SCOPE, "release-v2", TestContext.Current.CancellationToken);
        var replacement = (await store.GetActiveDefinitionAsync(
            SCOPE,
            oldTemplate.Revision.JobKey,
            TestContext.Current.CancellationToken))!;
        var replacementTemplate = replacement.CreateExecutionTemplate();
        await store.EnqueueAsync(
            CreateEnqueue("new-owner-queued", replacementTemplate, START_TIME),
            TestContext.Current.CancellationToken);
        var replacementCapability = await store.RegisterWorkerCapabilityAsync(
            new WorkerCapabilityRegistration
            {
                SchedulerScopeKey = SCOPE,
                OwnerKey = "owner-b",
                WorkerRevisionId = replacementTemplate.Revision.WorkerRevisionId,
                WorkerInstanceId = "new-replica",
                JobRevisionIds = [replacementTemplate.Revision.JobRevisionId]
            },
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        var whileOldOwnerRuns = await ClaimAllAsync(
            store,
            replacementCapability,
            1,
            TestContext.Current.CancellationToken);

        whileOldOwnerRuns.Should().BeEmpty();
        (await store.GetExecutionAsync(SCOPE, "new-owner-queued", TestContext.Current.CancellationToken))!
            .State.Should().Be(JobExecutionState.Queued);

        if (expireOldLease)
        {
            time.Advance(TimeSpan.FromMinutes(1));
            var recovered = await store.RecoverExpiredLeasesAsync(new ExpiredLeaseRecoveryRequest
            {
                SchedulerScopeKey = SCOPE,
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

        var afterOldLeaseResolved = await ClaimAllAsync(
            store,
            replacementCapability,
            1,
            TestContext.Current.CancellationToken);
        afterOldLeaseResolved.Should().ContainSingle();
        afterOldLeaseResolved[0].Execution.InstanceId.Should().Be("new-owner-queued");
        afterOldLeaseResolved[0].Execution.Template.Revision.OwnerKey.Should().Be("owner-b");
    }

    [Fact]
    public async Task EnqueueAsync_WhenIdenticalRequestIsRetriedDuringCatalogTransition_ShouldReturnCapturedExecution()
    {
        var store = new InMemoryJobSchedulerStore(new ManualTimeProvider(START_TIME));
        var active = await ActivateTriggeredDefinitionAsync(
            store,
            cancellationToken: TestContext.Current.CancellationToken);
        var request = CreateEnqueue("execution-1", active.CreateExecutionTemplate(), START_TIME);
        var first = await store.EnqueueAsync(request, TestContext.Current.CancellationToken);
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(
                new JobCatalogReleaseManifest(
                    SCOPE,
                    "release-v2",
                    [new JobCatalogOwnerManifest("owner-a", "worker-v2")]),
                2),
            TestContext.Current.CancellationToken);

        var retried = await store.EnqueueAsync(request, TestContext.Current.CancellationToken);

        retried.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task EnqueueAsync_WhenRetryOmitsAvailabilityAfterClockAdvances_ShouldReturnOriginalExecution()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var active = await ActivateTriggeredDefinitionAsync(
            store,
            cancellationToken: TestContext.Current.CancellationToken);
        var request = CreateEnqueue(
            "authoritative-availability",
            active.CreateExecutionTemplate(),
            START_TIME) with
        {
            AvailableAtUtc = null
        };

        var first = await store.EnqueueAsync(request, TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(5));
        var retry = await store.EnqueueAsync(request, TestContext.Current.CancellationToken);

        first.AvailableAtUtc.Should().Be(START_TIME);
        retry.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task ClaimAsync_WhenCatalogTransitionIsInProgress_ShouldLeaveOldExecutionQueued()
    {
        var store = new InMemoryJobSchedulerStore(new ManualTimeProvider(START_TIME));
        var active = await ActivateTriggeredDefinitionAsync(
            store,
            cancellationToken: TestContext.Current.CancellationToken);
        var template = active.CreateExecutionTemplate();
        await store.EnqueueAsync(
            CreateEnqueue("execution-1", template, START_TIME),
            TestContext.Current.CancellationToken);
        var capability = await RegisterWorkerAsync(
            store,
            "worker-1",
            template.Revision.WorkerRevisionId,
            [template.Revision.JobRevisionId],
            cancellationToken: TestContext.Current.CancellationToken);
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(
                new JobCatalogReleaseManifest(
                    SCOPE,
                    "release-v2",
                    [new JobCatalogOwnerManifest("owner-a", "worker-v2")]),
                2),
            TestContext.Current.CancellationToken);

        var claims = await ClaimAllAsync(
            store,
            capability,
            1,
            TestContext.Current.CancellationToken);

        claims.Should().BeEmpty();
        (await store.GetExecutionAsync(SCOPE, "execution-1", TestContext.Current.CancellationToken))!
            .State.Should().Be(JobExecutionState.Queued);
    }

    [Fact]
    public async Task ActivateAsync_WhenBacklogIsLarge_ShouldRetireAndRequestCancellationWithExactCounts()
    {
        const int QUEUED_BACKLOG_SIZE = 256;
        var store = new InMemoryJobSchedulerStore(new ManualTimeProvider(START_TIME));
        var active = await ActivateTriggeredDefinitionAsync(
            store,
            cancellationToken: TestContext.Current.CancellationToken);
        var template = active.CreateExecutionTemplate();
        await store.EnqueueAsync(
            CreateEnqueue("a-running", template, START_TIME),
            TestContext.Current.CancellationToken);
        for (var index = 0; index < QUEUED_BACKLOG_SIZE; index++)
        {
            await store.EnqueueAsync(
                CreateEnqueue($"z-backlog-{index:D4}", template, START_TIME),
                TestContext.Current.CancellationToken);
        }

        var capability = await RegisterWorkerAsync(
            store,
            "worker-1",
            template.Revision.WorkerRevisionId,
            [template.Revision.JobRevisionId],
            cancellationToken: TestContext.Current.CancellationToken);
        await ClaimOneAsync(store, capability, TestContext.Current.CancellationToken);
        var runningBeforeCutover = await store.GetExecutionAsync(
            SCOPE,
            "a-running",
            TestContext.Current.CancellationToken);
        var queuedBeforeCutover = await store.GetExecutionAsync(
            SCOPE,
            "z-backlog-0000",
            TestContext.Current.CancellationToken);
        var replacementOwner = new JobCatalogOwnerManifest("owner-a", "worker-v2");
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(
                new JobCatalogReleaseManifest(SCOPE, "release-v2", [replacementOwner]),
                2),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                SCOPE,
                "release-v2",
                replacementOwner.OwnerId,
                replacementOwner.WorkerRevisionId,
                [active.Declaration]),
            TestContext.Current.CancellationToken);

        var activation = await store.TryActivateReleaseAsync(
            SCOPE,
            "release-v2",
            TestContext.Current.CancellationToken);
        var statistics = await store.GetExecutionStateStatisticsAsync(
            SCOPE,
            cancellationToken: TestContext.Current.CancellationToken);
        var runningAfterCutover = await store.GetExecutionAsync(
            SCOPE,
            "a-running",
            TestContext.Current.CancellationToken);
        var queuedAfterCutover = await store.GetExecutionAsync(
            SCOPE,
            "z-backlog-0000",
            TestContext.Current.CancellationToken);

        activation.Activation!.RetiredQueuedExecutionCount.Should().Be(QUEUED_BACKLOG_SIZE);
        activation.Activation.RunningCancellationRequestCount.Should().Be(1);
        statistics[JobExecutionState.Cancelled].Should().Be(QUEUED_BACKLOG_SIZE);
        statistics[JobExecutionState.Running].Should().Be(1);
        queuedAfterCutover!.State.Should().Be(JobExecutionState.Cancelled);
        runningAfterCutover!.State.Should().Be(JobExecutionState.Running);
        runningAfterCutover.CancellationRequestedAtUtc.Should().Be(START_TIME);
        queuedAfterCutover!.History.Should().BeEquivalentTo(queuedBeforeCutover!.History);
        runningAfterCutover.History.Should().BeEquivalentTo(runningBeforeCutover!.History);
    }

    [Fact]
    public async Task CompleteAttemptAsync_WhenFailuresExhaustRetryPolicy_ShouldFenceOldAttemptsAndFailFinally()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var active = await ActivateTriggeredDefinitionAsync(
            store,
            retryCount: 1,
            cancellationToken: TestContext.Current.CancellationToken);
        var template = active.CreateExecutionTemplate();
        await store.EnqueueAsync(
            CreateEnqueue("execution-1", template, START_TIME),
            TestContext.Current.CancellationToken);
        var capability = await RegisterWorkerAsync(store, "worker-1", template.Revision.WorkerRevisionId,
            [template.Revision.JobRevisionId], cancellationToken: TestContext.Current.CancellationToken);
        var first = await ClaimOneAsync(store, capability, TestContext.Current.CancellationToken);

        var firstFailure = await store.CompleteAttemptAsync(new JobAttemptCompletion
        {
            LeaseKey = first.LeaseKey,
            Outcome = JobAttemptOutcome.Failed,
            Message = "first failure",
            RetryDelay = TimeSpan.FromMinutes(2)
        }, TestContext.Current.CancellationToken);
        var staleCompletion = await store.CompleteAttemptAsync(new JobAttemptCompletion
        {
            LeaseKey = first.LeaseKey,
            Outcome = JobAttemptOutcome.Succeeded
        }, TestContext.Current.CancellationToken);

        firstFailure.Execution!.State.Should().Be(JobExecutionState.Queued);
        firstFailure.Execution.RetryAttempt.Should().Be(1);
        staleCompletion.Status.Should().Be(JobAttemptCompletionStatus.Lost);
        (await ClaimAllAsync(store, capability, 1, TestContext.Current.CancellationToken)).Should().BeEmpty();

        time.Advance(TimeSpan.FromMinutes(2));
        var retry = await ClaimOneAsync(store, capability, TestContext.Current.CancellationToken);
        var finalFailure = await store.CompleteAttemptAsync(new JobAttemptCompletion
        {
            LeaseKey = retry.LeaseKey,
            Outcome = JobAttemptOutcome.Failed,
            Message = "second failure"
        }, TestContext.Current.CancellationToken);

        finalFailure.Execution!.State.Should().Be(JobExecutionState.Failed);
        finalFailure.Execution.RetryAttempt.Should().Be(2);
        finalFailure.Execution.CompletedAtUtc.Should().Be(time.GetUtcNow());
    }

    [Fact]
    public async Task RequestCancellationAsync_WhenQueuedOrRunning_ShouldPersistTheAppropriateOutcome()
    {
        var store = new InMemoryJobSchedulerStore(new ManualTimeProvider(START_TIME));
        var active = await ActivateTriggeredDefinitionAsync(
            store,
            maxConcurrency: 2,
            cancellationToken: TestContext.Current.CancellationToken);
        var template = active.CreateExecutionTemplate();
        await store.EnqueueAsync(
            CreateEnqueue("queued", template, START_TIME),
            TestContext.Current.CancellationToken);
        await store.EnqueueAsync(
            CreateEnqueue("running", template, START_TIME),
            TestContext.Current.CancellationToken);
        var capability = await RegisterWorkerAsync(store, "worker-1", template.Revision.WorkerRevisionId,
            [template.Revision.JobRevisionId], cancellationToken: TestContext.Current.CancellationToken);
        var running = (await ClaimAllAsync(
            store,
            capability,
            1,
            TestContext.Current.CancellationToken)).Single();
        running.Execution.InstanceId.Should().Be("queued");

        var queuedCancellation = await store.RequestCancellationAsync(
            SCOPE,
            "running",
            "operator cancelled",
            TestContext.Current.CancellationToken);
        var runningCancellation = await store.RequestCancellationAsync(
            SCOPE,
            "queued",
            "operator cancelled",
            TestContext.Current.CancellationToken);
        var renewal = await store.RenewLeaseAsync(
            running.LeaseKey,
            TimeSpan.FromMinutes(1),
            TestContext.Current.CancellationToken);
        var lateSuccess = await store.CompleteAttemptAsync(new JobAttemptCompletion
        {
            LeaseKey = running.LeaseKey,
            Outcome = JobAttemptOutcome.Succeeded
        }, TestContext.Current.CancellationToken);

        queuedCancellation.Status.Should().Be(JobCancellationStatus.Cancelled);
        queuedCancellation.Execution!.State.Should().Be(JobExecutionState.Cancelled);
        runningCancellation.Status.Should().Be(JobCancellationStatus.CancellationRequested);
        renewal.Status.Should().Be(JobLeaseRenewalStatus.CancellationRequested);
        lateSuccess.Execution!.State.Should().Be(JobExecutionState.Cancelled);
    }

    [Fact]
    public async Task RequestCancellationAsync_WhenRunningLeaseExpired_ShouldRetainWorkerInTerminalHistory()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var active = await ActivateTriggeredDefinitionAsync(
            store,
            cancellationToken: TestContext.Current.CancellationToken);
        var template = active.CreateExecutionTemplate();
        await store.EnqueueAsync(
            CreateEnqueue("execution-1", template, START_TIME),
            TestContext.Current.CancellationToken);
        var capability = await RegisterWorkerAsync(
            store,
            "worker-1",
            template.Revision.WorkerRevisionId,
            [template.Revision.JobRevisionId],
            cancellationToken: TestContext.Current.CancellationToken);
        _ = await ClaimOneAsync(
            store,
            capability,
            TestContext.Current.CancellationToken,
            TimeSpan.FromMinutes(1));
        time.Advance(TimeSpan.FromMinutes(1));

        var cancellation = await store.RequestCancellationAsync(
            SCOPE,
            "execution-1",
            cancellationToken: TestContext.Current.CancellationToken);

        cancellation.Status.Should().Be(JobCancellationStatus.Cancelled);
        cancellation.Execution!.History.Should().BeEmpty();
        var detail = await store.GetExecutionAsync(
            SCOPE,
            "execution-1",
            TestContext.Current.CancellationToken);
        detail!.History[^1].WorkerInstanceId.Should().Be("worker-1");
    }

    [Fact]
    public async Task RecoverExpiredLeasesAsync_WhenWorkerDisappears_ShouldRequeueAndFenceItsToken()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var active = await ActivateTriggeredDefinitionAsync(
            store,
            cancellationToken: TestContext.Current.CancellationToken);
        var template = active.CreateExecutionTemplate();
        await store.EnqueueAsync(
            CreateEnqueue("execution-1", template, START_TIME),
            TestContext.Current.CancellationToken);
        var firstCapability = await RegisterWorkerAsync(store, "worker-1", template.Revision.WorkerRevisionId,
            [template.Revision.JobRevisionId], TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);
        var firstLease = await ClaimOneAsync(
            store,
            firstCapability,
            TestContext.Current.CancellationToken,
            TimeSpan.FromMinutes(1));

        time.Advance(TimeSpan.FromMinutes(1));
        var recovered = await store.RecoverExpiredLeasesAsync(new ExpiredLeaseRecoveryRequest
        {
            SchedulerScopeKey = SCOPE,
            MaxCount = 10
        }, TestContext.Current.CancellationToken);
        var staleLog = await store.AppendExecutionLogAsync(new JobExecutionLogEntry
        {
            LeaseKey = firstLease.LeaseKey,
            Message = "must be fenced",
            LogLevel = LogLevel.Warning
        }, TestContext.Current.CancellationToken);

        recovered.Should().ContainSingle();
        recovered[0].State.Should().Be(JobExecutionState.Queued);
        recovered[0].LeaseLossCount.Should().Be(1);
        staleLog.Should().Be(JobLeaseMutationStatus.Lost);

        var secondCapability = await RegisterWorkerAsync(store, "worker-2", template.Revision.WorkerRevisionId,
            [template.Revision.JobRevisionId], cancellationToken: TestContext.Current.CancellationToken);
        var secondLease = await ClaimOneAsync(store, secondCapability, TestContext.Current.CancellationToken);
        secondLease.LeaseKey.LeaseToken.Should().NotBe(firstLease.LeaseKey.LeaseToken);
    }

    [Fact]
    public async Task SynchronizeRecurringScheduleAsync_WhenFirstSyncIsDelayed_ShouldMaterializeFromActivationBoundary()
    {
        var activationTime = new DateTimeOffset(2026, 8, 13, 10, 0, 0, TimeSpan.Zero);
        var time = new ManualTimeProvider(activationTime);
        var store = new InMemoryJobSchedulerStore(time);
        var active = await ActivateRecurringDefinitionAsync(
            store,
            TestContext.Current.CancellationToken,
            "0 0 * * * *");
        time.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(5)));
        var version = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);

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
            SCOPE,
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
    public async Task SynchronizeRecurringScheduleAsync_WhenDebugReasonChangesAtSameEpoch_ShouldUpdateAndResume()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var active = await ActivateRecurringDefinitionAsync(store, TestContext.Current.CancellationToken);
        var version = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);
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
        var suspended = await store.SynchronizeRecurringScheduleAsync(
            synchronization with { SuspensionReasons = JobRecurringScheduleSuspensionReason.DebugMode },
            TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(5));
        var resumed = await store.SynchronizeRecurringScheduleAsync(
            synchronization,
            TestContext.Current.CancellationToken);
        var unchanged = await store.SynchronizeRecurringScheduleAsync(
            synchronization,
            TestContext.Current.CancellationToken);

        created.Status.Should().Be(RecurringScheduleSynchronizationStatus.Created);
        suspended.Status.Should().Be(RecurringScheduleSynchronizationStatus.Updated);
        suspended.Cursor!.SuspensionReasons.Should().Be(JobRecurringScheduleSuspensionReason.DebugMode);
        suspended.Cursor.IsSuspended.Should().BeTrue();
        suspended.Cursor.NextOccurrenceUtc.Should().BeNull();
        resumed.Status.Should().Be(RecurringScheduleSynchronizationStatus.Updated);
        resumed.Cursor!.SuspensionReasons.Should().Be(JobRecurringScheduleSuspensionReason.None);
        resumed.Cursor.IsSuspended.Should().BeFalse();
        resumed.Cursor.NextOccurrenceUtc.Should().Be(START_TIME.AddMinutes(6).AddSeconds(-3));
        unchanged.Status.Should().Be(RecurringScheduleSynchronizationStatus.Unchanged);
        unchanged.Cursor!.Version.Should().Be(resumed.Cursor.Version);
    }

    [Fact]
    public async Task SynchronizeRecurringScheduleAsync_ShouldDeriveOperatorPolicyReasonFromActivePolicy()
    {
        var store = new InMemoryJobSchedulerStore(new ManualTimeProvider(START_TIME));
        var active = await ActivateRecurringDefinitionAsync(store, TestContext.Current.CancellationToken);
        var version = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);
        var synchronization = new RecurringScheduleSynchronization
        {
            Template = active.CreateExecutionTemplate(),
            Schedule = new RecurringScheduleDefinition
            {
                CronExpression = active.Declaration.CronExpression!,
                TimeZoneId = active.Declaration.TimeZoneId!
            },
            ChangeEpoch = version.ChangeEpoch,
            SuspensionReasons = JobRecurringScheduleSuspensionReason.OperatorPolicy
        };

        var callerPolicyReasonCleared = await store.SynchronizeRecurringScheduleAsync(
            synchronization,
            TestContext.Current.CancellationToken);
        var disabledPolicy = await store.UpdatePolicyAsync(
            SCOPE,
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
        version = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);
        var operatorAndDebugSuspended = await store.SynchronizeRecurringScheduleAsync(
            synchronization with
            {
                ChangeEpoch = version.ChangeEpoch,
                SuspensionReasons = JobRecurringScheduleSuspensionReason.DebugMode
            },
            TestContext.Current.CancellationToken);
        _ = await store.UpdatePolicyAsync(
            SCOPE,
            active.OwnerId,
            active.Declaration.JobKey,
            new JobPolicyChange
            {
                DisabledOverride = false,
                MaxRetainedHistoryRecords = disabledPolicy.MaxRetainedHistoryRecords,
                MaxRetentionDays = disabledPolicy.MaxRetentionDays,
                ExpectedConcurrencyStamp = disabledPolicy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        version = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);
        var operatorReasonCleared = await store.SynchronizeRecurringScheduleAsync(
            synchronization with
            {
                ChangeEpoch = version.ChangeEpoch,
                SuspensionReasons = JobRecurringScheduleSuspensionReason.OperatorPolicy
                                    | JobRecurringScheduleSuspensionReason.DebugMode
            },
            TestContext.Current.CancellationToken);

        callerPolicyReasonCleared.Cursor!.SuspensionReasons
            .Should().Be(JobRecurringScheduleSuspensionReason.None);
        operatorAndDebugSuspended.Cursor!.SuspensionReasons.Should().Be(
            JobRecurringScheduleSuspensionReason.OperatorPolicy
            | JobRecurringScheduleSuspensionReason.DebugMode);
        operatorReasonCleared.Cursor!.SuspensionReasons.Should().Be(JobRecurringScheduleSuspensionReason.DebugMode);
    }

    [Fact]
    public async Task SynchronizeRecurringScheduleAsync_WhenTemplateJobNameIsEmpty_ShouldRejectTemplate()
    {
        var store = new InMemoryJobSchedulerStore(new ManualTimeProvider(START_TIME));
        var active = await ActivateRecurringDefinitionAsync(store, TestContext.Current.CancellationToken);
        var version = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);

        var synchronize = async () => await store.SynchronizeRecurringScheduleAsync(
            new RecurringScheduleSynchronization
            {
                Template = active.CreateExecutionTemplate() with { JobName = string.Empty },
                Schedule = new RecurringScheduleDefinition
                {
                    CronExpression = active.Declaration.CronExpression!,
                    TimeZoneId = active.Declaration.TimeZoneId!
                },
                ChangeEpoch = version.ChangeEpoch
            },
            TestContext.Current.CancellationToken);

        await synchronize.Should().ThrowAsync<ArgumentException>()
            .WithParameterName(nameof(JobExecutionTemplate.JobName));
    }

    [Fact]
    public async Task TryMaterializeRecurringOccurrenceAsync_WhenOutstandingCapacityIsFull_ShouldRecordSkippedOccurrence()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var active = await ActivateRecurringDefinitionAsync(store, TestContext.Current.CancellationToken);
        var catalogVersion = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);
        var synchronized = await store.SynchronizeRecurringScheduleAsync(new RecurringScheduleSynchronization
        {
            Template = active.CreateExecutionTemplate(),
            Schedule = new RecurringScheduleDefinition
            {
                CronExpression = active.Declaration.CronExpression!,
                TimeZoneId = active.Declaration.TimeZoneId!
            },
            ChangeEpoch = catalogVersion.ChangeEpoch
        }, TestContext.Current.CancellationToken);
        var firstOccurrence = synchronized.Cursor!.NextOccurrenceUtc!.Value;
        time.Advance(firstOccurrence - START_TIME);
        var first = await store.TryMaterializeRecurringOccurrenceAsync(new RecurringOccurrenceMaterialization
        {
            CursorKey = synchronized.Cursor.Key,
            ExpectedVersion = synchronized.Cursor.Version,
            ExpectedOccurrenceUtc = firstOccurrence,
            NextOccurrenceUtc = firstOccurrence.AddMinutes(1),
            InstanceId = "recurring-admitted"
        }, TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(1));

        var skipped = await store.TryMaterializeRecurringOccurrenceAsync(new RecurringOccurrenceMaterialization
        {
            CursorKey = first.Cursor!.Key,
            ExpectedVersion = first.Cursor.Version,
            ExpectedOccurrenceUtc = firstOccurrence.AddMinutes(1),
            NextOccurrenceUtc = firstOccurrence.AddMinutes(2),
            InstanceId = "recurring-skipped"
        }, TestContext.Current.CancellationToken);
        var detail = await store.GetExecutionAsync(
            SCOPE,
            "recurring-skipped",
            TestContext.Current.CancellationToken);
        var statistics = await store.GetExecutionStateStatisticsAsync(
            SCOPE,
            cancellationToken: TestContext.Current.CancellationToken);
        var cancellation = await store.RequestCancellationAsync(
            SCOPE,
            "recurring-skipped",
            cancellationToken: TestContext.Current.CancellationToken);

        first.Execution!.State.Should().Be(JobExecutionState.Queued);
        skipped.Status.Should().Be(RecurringMaterializationStatus.Materialized);
        skipped.Execution!.State.Should().Be(JobExecutionState.Skipped);
        skipped.Execution.IsTerminal.Should().BeTrue();
        skipped.Execution.Origin.Should().Be(JobExecutionOrigin.RecurringSchedule);
        skipped.Execution.RecurringOccurrenceUtc.Should().Be(firstOccurrence.AddMinutes(1));
        skipped.Execution.SkipReason.Should().Be(JobExecutionSkipReason.RecurringCapacityUnavailable);
        skipped.Execution.StartedAtUtc.Should().BeNull();
        skipped.Execution.CompletedAtUtc.Should().Be(time.GetUtcNow());
        skipped.Execution.ExecutionAttempt.Should().Be(0);
        skipped.Cursor!.NextOccurrenceUtc.Should().Be(firstOccurrence.AddMinutes(2));
        detail!.History.Should().ContainSingle(entry =>
            entry.NewState == JobExecutionState.Skipped && entry.LogLevel == LogLevel.Warning);
        statistics[JobExecutionState.Skipped].Should().Be(1);
        cancellation.Status.Should().Be(JobCancellationStatus.AlreadyTerminal);
        (await store.DeleteExecutionsAsync(
            SCOPE,
            ["recurring-skipped"],
            TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task RunRecurringNowAsync_WhenRepeated_ShouldBeIdempotentAndPreserveScheduleCursor()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var active = await ActivateRecurringDefinitionAsync(store, TestContext.Current.CancellationToken);
        var catalogVersion = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);
        var synchronized = await store.SynchronizeRecurringScheduleAsync(new RecurringScheduleSynchronization
        {
            Template = active.CreateExecutionTemplate(),
            Schedule = new RecurringScheduleDefinition
            {
                CronExpression = active.Declaration.CronExpression!,
                TimeZoneId = active.Declaration.TimeZoneId!
            },
            ChangeEpoch = catalogVersion.ChangeEpoch
        }, TestContext.Current.CancellationToken);
        var command = new JobRecurringRunNowCommand
        {
            InstanceId = "recurring-run-now",
            SchedulerScopeKey = SCOPE,
            JobKey = active.Declaration.JobKey,
            ExpectedOwnerId = active.OwnerId,
            ExpectedJobRevisionId = active.JobRevisionId
        };

        var first = await store.RunRecurringNowAsync(command, TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(1));
        var repeated = await store.RunRecurringNowAsync(command, TestContext.Current.CancellationToken);
        var summary = await store.GetOperationalSummaryAsync(
            SCOPE,
            active.Declaration.JobKey,
            TestContext.Current.CancellationToken);

        first.State.Should().Be(JobExecutionState.Queued);
        first.Origin.Should().Be(JobExecutionOrigin.RecurringRunNow);
        first.RecurringOccurrenceUtc.Should().BeNull();
        repeated.Should().BeEquivalentTo(first);
        summary!.NextOccurrenceUtc.Should().Be(synchronized.Cursor!.NextOccurrenceUtc);
    }

    [Fact]
    public async Task QueryOperationalSummariesAsync_ShouldProjectRecurringExecutionAndWorkerState()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var active = await ActivateRecurringDefinitionAsync(store, TestContext.Current.CancellationToken);
        var version = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);
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
        time.Advance(occurrence - START_TIME);
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
        await RegisterWorkerAsync(
            store,
            "operational-worker",
            active.WorkerRevisionId,
            [active.JobRevisionId],
            cancellationToken: TestContext.Current.CancellationToken);

        var page = await store.QueryOperationalSummariesAsync(
            SCOPE,
            new JobCatalogQuery(),
            TestContext.Current.CancellationToken);

        var summary = page.Items.Should().ContainSingle().Subject;
        summary.RecurringScheduleStatus.Should().Be(JobRecurringScheduleStatus.Scheduled);
        summary.SuspensionReasons.Should().Be(JobRecurringScheduleSuspensionReason.None);
        summary.IsSuspended.Should().BeFalse();
        summary.NextOccurrenceUtc.Should().Be(nextOccurrence);
        summary.LatestExecution!.InstanceId.Should().Be(materialized.Execution!.InstanceId);
        summary.LatestExecution.History.Should().BeEmpty();
        summary.QueuedExecutionCount.Should().Be(1);
        summary.RunningExecutionCount.Should().Be(0);
        summary.ActiveExecutionCount.Should().Be(1);
        summary.CompatibleWorkerCount.Should().Be(1);
        (await store.GetOperationalSummaryAsync(
            SCOPE,
            active.Declaration.JobKey,
            TestContext.Current.CancellationToken)).Should().BeEquivalentTo(summary);
        (await store.GetOperationalSummaryAsync(
            SCOPE,
            "jobs.missing",
            TestContext.Current.CancellationToken)).Should().BeNull();

        await store.UpdatePolicyAsync(
            SCOPE,
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
            SCOPE,
            new JobCatalogQuery(),
            TestContext.Current.CancellationToken)).Items.Single();
        suspended.RecurringScheduleStatus.Should().Be(JobRecurringScheduleStatus.Suspended);
        suspended.SuspensionReasons.Should().Be(JobRecurringScheduleSuspensionReason.OperatorPolicy);
        suspended.IsSuspended.Should().BeTrue();
        suspended.NextOccurrenceUtc.Should().BeNull();
    }

    [Fact]
    public async Task ActivateAsync_WhenRecurringEpochIsSuperseded_ShouldPruneItsCursor()
    {
        var store = new InMemoryJobSchedulerStore(new ManualTimeProvider(START_TIME));
        var active = await ActivateRecurringDefinitionAsync(store, TestContext.Current.CancellationToken);
        var version = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);
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
        var replacementOwner = new JobCatalogOwnerManifest("owner-a", "worker-v2");
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(
                new JobCatalogReleaseManifest(SCOPE, "release-v2", [replacementOwner]),
                2),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                SCOPE,
                "release-v2",
                replacementOwner.OwnerId,
                replacementOwner.WorkerRevisionId,
                [active.Declaration]),
            TestContext.Current.CancellationToken);

        await store.TryActivateReleaseAsync(SCOPE, "release-v2", TestContext.Current.CancellationToken);
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
    public async Task SynchronizeRecurringScheduleAsync_WhenCallerClockIsSkewed_ShouldCreateAndResumeFromStoreClock()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var active = await ActivateRecurringDefinitionAsync(store, TestContext.Current.CancellationToken);
        var schedule = new RecurringScheduleDefinition
        {
            CronExpression = "0 * * * * *",
            TimeZoneId = TimeZoneInfo.Utc.Id
        };
        var version = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);
        var synchronization = new RecurringScheduleSynchronization
        {
            Template = active.CreateExecutionTemplate(),
            Schedule = schedule,
            ChangeEpoch = version.ChangeEpoch
        };

        var created = await store.SynchronizeRecurringScheduleAsync(
            synchronization,
            TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(10));
        var retentionPolicy = await store.UpdatePolicyAsync(
            SCOPE,
            active.OwnerId,
            active.Declaration.JobKey,
            new JobPolicyChange
            {
                MaxRetainedHistoryRecords = 50,
                ExpectedConcurrencyStamp = active.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        version = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);
        var preserved = await store.SynchronizeRecurringScheduleAsync(
            synchronization with { ChangeEpoch = version.ChangeEpoch },
            TestContext.Current.CancellationToken);
        var disabledPolicy = await store.UpdatePolicyAsync(
            SCOPE,
            active.OwnerId,
            active.Declaration.JobKey,
            new JobPolicyChange
            {
                DisabledOverride = true,
                MaxRetainedHistoryRecords = 50,
                ExpectedConcurrencyStamp = retentionPolicy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        version = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);
        var suspended = await store.SynchronizeRecurringScheduleAsync(
            synchronization with { ChangeEpoch = version.ChangeEpoch },
            TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(5));
        _ = await store.UpdatePolicyAsync(
            SCOPE,
            active.OwnerId,
            active.Declaration.JobKey,
            new JobPolicyChange
            {
                DisabledOverride = false,
                MaxRetainedHistoryRecords = 50,
                ExpectedConcurrencyStamp = disabledPolicy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        version = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);
        var resumed = await store.SynchronizeRecurringScheduleAsync(
            synchronization with { ChangeEpoch = version.ChangeEpoch },
            TestContext.Current.CancellationToken);

        created.Cursor!.NextOccurrenceUtc.Should().Be(START_TIME.AddMinutes(1).AddSeconds(-3));
        preserved.Cursor!.NextOccurrenceUtc.Should().Be(created.Cursor.NextOccurrenceUtc);
        suspended.Cursor!.IsSuspended.Should().BeTrue();
        suspended.Cursor.SuspensionReasons.Should().Be(JobRecurringScheduleSuspensionReason.OperatorPolicy);
        suspended.Cursor.NextOccurrenceUtc.Should().BeNull();
        resumed.Cursor!.IsSuspended.Should().BeFalse();
        resumed.Cursor.SuspensionReasons.Should().Be(JobRecurringScheduleSuspensionReason.None);
        resumed.Cursor.NextOccurrenceUtc.Should().Be(START_TIME.AddMinutes(16).AddSeconds(-3));
    }

    [Fact]
    public async Task TryMaterializeRecurringOccurrenceAsync_WhenLeadersRace_ShouldInsertExactlyOnce()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var active = await ActivateRecurringDefinitionAsync(store, TestContext.Current.CancellationToken);
        var template = active.CreateExecutionTemplate();
        var version = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);
        var synchronized = await store.SynchronizeRecurringScheduleAsync(new RecurringScheduleSynchronization
        {
            Template = template,
            Schedule = new RecurringScheduleDefinition
            {
                CronExpression = "0 * * * * *",
                TimeZoneId = TimeZoneInfo.Utc.Id,
                StartTimeUtc = START_TIME.AddDays(-1),
                EndTimeUtc = START_TIME.AddDays(1)
            },
            ChangeEpoch = version.ChangeEpoch
        }, TestContext.Current.CancellationToken);
        var cursor = synchronized.Cursor!;
        var occurrence = cursor.NextOccurrenceUtc!.Value;
        time.Advance(occurrence - START_TIME);
        var request = new RecurringOccurrenceMaterialization
        {
            CursorKey = cursor.Key,
            ExpectedVersion = cursor.Version,
            ExpectedOccurrenceUtc = occurrence,
            NextOccurrenceUtc = occurrence.AddMinutes(1),
            InstanceId = "recurring-1"
        };

        var first = await store.TryMaterializeRecurringOccurrenceAsync(
            request,
            TestContext.Current.CancellationToken);
        var duplicate = await store.TryMaterializeRecurringOccurrenceAsync(
            request,
            TestContext.Current.CancellationToken);
        var history = await store.QueryExecutionsAsync(new JobExecutionQuery
        {
            SchedulerScopeKey = SCOPE,
            PageSize = 10
        }, TestContext.Current.CancellationToken);

        first.Status.Should().Be(RecurringMaterializationStatus.Materialized);
        first.Cursor!.Version.Should().Be(cursor.Version + 1);
        duplicate.Status.Should().Be(RecurringMaterializationStatus.StaleCursor);
        duplicate.Execution.Should().BeNull();
        duplicate.Cursor!.Version.Should().Be(cursor.Version + 1);
        duplicate.Cursor.NextOccurrenceUtc.Should().Be(occurrence.AddMinutes(1));
        history.TotalCount.Should().Be(1);
        history.Items[0].AvailableAtUtc.Should().Be(occurrence);
        first.Cursor.Schedule.CronExpression.Should().Be("0 * * * * *");
    }

    [Fact]
    public async Task GetDueRecurringSchedulesAsync_WhenOneCursorAdvances_ShouldOfferOtherOverdueCursorNext()
    {
        var time = new ManualTimeProvider(START_TIME);
        var store = new InMemoryJobSchedulerStore(time);
        var owner = new JobCatalogOwnerManifest("owner-a", "worker-v1");
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(
                new JobCatalogReleaseManifest(SCOPE, "release-v1", [owner]),
                1),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                SCOPE,
                "release-v1",
                owner.OwnerId,
                owner.WorkerRevisionId,
                [CreateRecurringDeclaration("jobs.alpha"), CreateRecurringDeclaration("jobs.beta")]),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(SCOPE, "release-v1", TestContext.Current.CancellationToken);
        var catalog = (await store.GetActiveCatalogAsync(SCOPE, TestContext.Current.CancellationToken))!;
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
        time.Advance(TimeSpan.FromMinutes(1));

        var first = (await store.GetDueRecurringSchedulesAsync(
            SCOPE,
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
            SCOPE,
            1,
            TestContext.Current.CancellationToken)).Single();

        second.Key.JobRevisionId.Should().NotBe(first.Key.JobRevisionId);
        second.NextOccurrenceUtc.Should().Be(occurrence);
    }

    private const string SCOPE = "execution-tests";

    private static JobEnqueueRequest CreateEnqueue(
        string instanceId,
        JobExecutionTemplate template,
        DateTimeOffset availableAtUtc)
    {
        return new JobEnqueueRequest
        {
            InstanceId = instanceId,
            SchedulerScopeKey = template.Revision.SchedulerScopeKey,
            JobKey = template.Revision.JobKey,
            ExpectedOwnerId = template.Revision.OwnerKey,
            ExpectedJobRevisionId = template.Revision.JobRevisionId,
            JobArgs = "{}",
            AvailableAtUtc = availableAtUtc
        };
    }

    private static WorkerCapabilityRegistration CreateWorkerRegistration(
        string schedulerScopeKey,
        string workerInstanceId) => new()
    {
        SchedulerScopeKey = schedulerScopeKey,
        OwnerKey = "owner-a",
        WorkerRevisionId = "worker-v1",
        WorkerInstanceId = workerInstanceId
    };

    private static Task<WorkerCapabilityLease> RegisterWorkerAsync(
        InMemoryJobSchedulerStore store,
        string workerInstanceId,
        string workerRevisionId,
        IReadOnlyCollection<string> jobRevisionIds,
        TimeSpan? leaseDuration = null,
        CancellationToken cancellationToken = default)
    {
        return store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = SCOPE,
            OwnerKey = "owner-a",
            WorkerRevisionId = workerRevisionId,
            WorkerInstanceId = workerInstanceId,
            JobRevisionIds = jobRevisionIds
        }, leaseDuration ?? TimeSpan.FromMinutes(5), cancellationToken);
    }

    private static async Task<JobExecutionLease> ClaimOneAsync(
        InMemoryJobSchedulerStore store,
        WorkerCapabilityLease capability,
        CancellationToken cancellationToken,
        TimeSpan? leaseDuration = null)
    {
        return (await ClaimAllAsync(store, capability, 1, cancellationToken, leaseDuration)).Single();
    }

    private static Task<IReadOnlyList<JobExecutionLease>> ClaimAllAsync(
        InMemoryJobSchedulerStore store,
        WorkerCapabilityLease capability,
        int maxCount,
        CancellationToken cancellationToken,
        TimeSpan? leaseDuration = null)
    {
        return store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = capability.LeaseKey,
            MaxCount = maxCount,
            LeaseDuration = leaseDuration ?? TimeSpan.FromMinutes(1)
        }, cancellationToken);
    }

    private static async Task<ActiveJobDefinition> ActivateRecurringDefinitionAsync(
        InMemoryJobSchedulerStore store,
        CancellationToken cancellationToken,
        string cronExpression = "0 * * * * *")
    {
        var owner = new JobCatalogOwnerManifest("owner-a", "worker-v1");
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(
                new JobCatalogReleaseManifest(SCOPE, "release-v1", [owner]),
                1),
            cancellationToken);
        await store.PublishOwnerSnapshotAsync(new JobOwnerCatalogSnapshot(
            SCOPE,
            "release-v1",
            owner.OwnerId,
            owner.WorkerRevisionId,
            [
                new JobDeclaration
                {
                    JobKey = "jobs.recurring",
                    JobName = "Recurring",
                    JobType = JobType.Recurring,
                    CronExpression = cronExpression,
                    TimeZoneId = TimeZoneInfo.Utc.Id,
                    MaxConcurrency = 1,
                    MaxExecutionTimeout = TimeSpan.FromMinutes(5)
                }
            ]),
            cancellationToken);
        await store.TryActivateReleaseAsync(SCOPE, "release-v1", cancellationToken);
        return (await store.GetActiveDefinitionAsync(SCOPE, "jobs.recurring", cancellationToken))!;
    }

    private static JobDeclaration CreateRecurringDeclaration(string jobKey) => new()
    {
        JobKey = jobKey,
        JobName = jobKey,
        JobType = JobType.Recurring,
        CronExpression = "0 * * * * *",
        TimeZoneId = TimeZoneInfo.Utc.Id,
        MaxConcurrency = 1,
        MaxExecutionTimeout = TimeSpan.FromMinutes(5)
    };

    private static JobDeclaration CreateTriggeredDeclaration(string jobKey) => new()
    {
        JobKey = jobKey,
        JobArgsKey = $"{jobKey}.Args",
        JobName = jobKey,
        JobType = JobType.Triggered,
        MaxConcurrency = 1,
        MaxExecutionTimeout = TimeSpan.FromMinutes(5)
    };

    private static async Task<ActiveJobDefinition> ActivateTriggeredDefinitionAsync(
        InMemoryJobSchedulerStore store,
        string workerRevisionId = "worker-v1",
        int maxConcurrency = 1,
        int retryCount = 0,
        CancellationToken cancellationToken = default)
    {
        var owner = new JobCatalogOwnerManifest("owner-a", workerRevisionId);
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(
                new JobCatalogReleaseManifest(SCOPE, "release-v1", [owner]),
                1),
            cancellationToken);
        await store.PublishOwnerSnapshotAsync(new JobOwnerCatalogSnapshot(
            SCOPE,
            "release-v1",
            owner.OwnerId,
            owner.WorkerRevisionId,
            [
                new JobDeclaration
                {
                    JobKey = "jobs.alpha",
                    JobArgsKey = "Jobs.AlphaArgs",
                    JobName = "Alpha",
                    JobType = JobType.Triggered,
                    MaxConcurrency = maxConcurrency,
                    RetryCount = retryCount,
                    MaxExecutionTimeout = TimeSpan.FromMinutes(5)
                }
            ]),
            cancellationToken);
        await store.TryActivateReleaseAsync(SCOPE, "release-v1", cancellationToken);
        return (await store.GetActiveDefinitionAsync(SCOPE, "jobs.alpha", cancellationToken))!;
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            _utcNow = _utcNow.Add(duration);
        }
    }
}
