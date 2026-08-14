using AwesomeAssertions;
using Monica.JobScheduler.Exceptions.Catalog;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Providers;
using Xunit;

namespace Test.Monica.JobScheduler.Stores.Catalog;

public sealed class InMemoryJobSchedulerCatalogStoreTests
{
    private const string SCOPE = "catalog-store-tests";
    private const string OWNER = "worker-a";

    [Fact]
    public async Task ReleaseLifecycle_WhenNewGenerationReusesOldRelease_ShouldCreateANewActivationAndPreservePolicy()
    {
        var store = CreateStore();
        await PublishCompleteReleaseAsync(store, "release-1", "revision-1", "Jobs.Daily", 1);
        var first = await store.TryActivateReleaseAsync(SCOPE, "release-1", TestContext.Current.CancellationToken);
        var active = await store.GetActiveDefinitionAsync(SCOPE, "Jobs.Daily", TestContext.Current.CancellationToken);
        var changedPolicy = await store.UpdatePolicyAsync(
            SCOPE,
            OWNER,
            "Jobs.Daily",
            new JobPolicyChange
            {
                DisabledOverride = true,
                MaxRetainedHistoryRecords = 25,
                MaxRetentionDays = 7,
                ExpectedConcurrencyStamp = active!.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        await PublishCompleteReleaseAsync(store, "release-2", "revision-2", "Jobs.Daily", 2);
        var second = await store.TryActivateReleaseAsync(SCOPE, "release-2", TestContext.Current.CancellationToken);
        var rollbackIntent = await store.StageReleaseAsync(
            Stage("release-1", "revision-1", 3),
            TestContext.Current.CancellationToken);
        var rollback = await store.TryActivateReleaseAsync(SCOPE, "release-1", TestContext.Current.CancellationToken);
        var restored = await store.GetActiveDefinitionAsync(SCOPE, "Jobs.Daily", TestContext.Current.CancellationToken);

        first.Status.Should().Be(JobCatalogActivationStatus.Activated);
        second.Version.ActivationEpoch.Should().Be(2);
        rollbackIntent.Status.Should().Be(ReleaseStageStatus.StagedAsDesired);
        rollback.Status.Should().Be(JobCatalogActivationStatus.Activated);
        rollback.Version.ActivationEpoch.Should().Be(3);
        restored!.Policy.Should().Be(changedPolicy);
        restored.JobRevisionId.Should().Be(restored.CreateExecutionTemplate().Revision.JobRevisionId);
        restored.CreateExecutionTemplate().JobName.Should().Be(restored.Declaration.JobName);
    }

    [Fact]
    public async Task Policy_WhenJobMovesOwnersOrIsTemporarilyRemoved_ShouldFollowTheLogicalJobKey()
    {
        var store = CreateStore();
        await PublishCompleteReleaseAsync(store, "release-1", "revision-1", "Jobs.Daily", 1);
        await store.TryActivateReleaseAsync(SCOPE, "release-1", TestContext.Current.CancellationToken);
        var first = (await store.GetActiveDefinitionAsync(
            SCOPE,
            "Jobs.Daily",
            TestContext.Current.CancellationToken))!;
        var firstPolicy = await store.UpdatePolicyAsync(
            SCOPE,
            OWNER,
            "Jobs.Daily",
            new JobPolicyChange
            {
                DisabledOverride = true,
                MaxRetainedHistoryRecords = 25,
                ExpectedConcurrencyStamp = first.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        const string replacementOwner = "worker-b";
        await PublishCompleteReleaseAsync(
            store,
            "release-2",
            "revision-2",
            "Jobs.Daily",
            2,
            replacementOwner);
        await store.TryActivateReleaseAsync(SCOPE, "release-2", TestContext.Current.CancellationToken);
        var moved = (await store.GetActiveDefinitionAsync(
            SCOPE,
            "Jobs.Daily",
            TestContext.Current.CancellationToken))!;

        moved.OwnerId.Should().Be(replacementOwner);
        moved.Policy.Should().Be(firstPolicy);
        await store.Invoking(candidate => candidate.UpdatePolicyAsync(
                SCOPE,
                OWNER,
                "Jobs.Daily",
                new JobPolicyChange { ExpectedConcurrencyStamp = moved.Policy.ConcurrencyStamp },
                TestContext.Current.CancellationToken))
            .Should().ThrowAsync<JobCatalogNotFoundException>();

        var replacementPolicy = await store.UpdatePolicyAsync(
            SCOPE,
            replacementOwner,
            "Jobs.Daily",
            new JobPolicyChange
            {
                DisabledOverride = false,
                MaxRetainedHistoryRecords = 12,
                ExpectedConcurrencyStamp = moved.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        const string emptyOwner = "worker-c";
        await store.StageReleaseAsync(
            Stage("release-3", "revision-3", 3, emptyOwner),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            SnapshotForOwner("release-3", "revision-3", emptyOwner),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(SCOPE, "release-3", TestContext.Current.CancellationToken);

        (await store.GetActiveDefinitionAsync(
                SCOPE,
                "Jobs.Daily",
                TestContext.Current.CancellationToken))
            .Should().BeNull();

        await store.ReactivateReleaseAsync(SCOPE, "release-1", TestContext.Current.CancellationToken);
        var restored = (await store.GetActiveDefinitionAsync(
            SCOPE,
            "Jobs.Daily",
            TestContext.Current.CancellationToken))!;

        restored.OwnerId.Should().Be(OWNER);
        restored.Policy.Should().Be(replacementPolicy);
    }

    [Fact]
    public async Task StageReleaseAsync_WhenOlderDeploymentArrivesLate_ShouldNotReplaceDesiredRelease()
    {
        var store = CreateStore();
        await PublishCompleteReleaseAsync(store, "release-2", "revision-2", "Jobs.Daily", 2);
        var active = await store.TryActivateReleaseAsync(SCOPE, "release-2", TestContext.Current.CancellationToken);

        var delayed = await store.StageReleaseAsync(
            Stage("release-1", "revision-1", 1),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            Snapshot("release-1", "revision-1", Declaration("Jobs.Daily")),
            TestContext.Current.CancellationToken);
        var delayedActivation = await store.TryActivateReleaseAsync(
            SCOPE,
            "release-1",
            TestContext.Current.CancellationToken);

        active.Status.Should().Be(JobCatalogActivationStatus.Activated);
        delayed.Status.Should().Be(ReleaseStageStatus.StagedAsSuperseded);
        delayed.Version.DesiredReleaseId.Should().Be("release-2");
        delayedActivation.Status.Should().Be(JobCatalogActivationStatus.NotDesired);
    }

    [Fact]
    public async Task ReactivateReleaseAsync_WhenControlLoopRestagesDeployment_ShouldPreserveManualIntent()
    {
        var store = CreateStore();
        await PublishCompleteReleaseAsync(store, "release-1", "revision-1", "Jobs.Daily", 1);
        await store.TryActivateReleaseAsync(SCOPE, "release-1", TestContext.Current.CancellationToken);
        await PublishCompleteReleaseAsync(store, "release-2", "revision-2", "Jobs.Daily", 2);
        await store.TryActivateReleaseAsync(SCOPE, "release-2", TestContext.Current.CancellationToken);

        var rollback = await store.ReactivateReleaseAsync(
            SCOPE,
            "release-1",
            TestContext.Current.CancellationToken);
        var controlLoopRestage = await store.StageReleaseAsync(
            Stage("release-2", "revision-2", 2),
            TestContext.Current.CancellationToken);

        rollback.ReleaseId.Should().Be("release-1");
        controlLoopRestage.Status.Should().Be(ReleaseStageStatus.Duplicate);
        controlLoopRestage.Version.DesiredReleaseId.Should().Be("release-1");
        controlLoopRestage.Version.ActiveReleaseId.Should().Be("release-1");
        controlLoopRestage.Version.IsTransitionInProgress.Should().BeFalse();
    }

    [Fact]
    public async Task PublicationStatus_ShouldExposeDesiredIntentAndMissingOwnersWithoutChangingCatalogEpoch()
    {
        var store = CreateStore();
        var stage = await store.StageReleaseAsync(
            Stage("release-1", "revision-1", 1),
            TestContext.Current.CancellationToken);

        var beforePublish = await store.GetCatalogPublicationStatusAsync(
            SCOPE,
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            Snapshot("release-1", "revision-1", Declaration("Jobs.Daily")),
            TestContext.Current.CancellationToken);
        var afterPublish = await store.GetCatalogPublicationStatusAsync(
            SCOPE,
            TestContext.Current.CancellationToken);

        stage.Version.ChangeEpoch.Should().Be(0);
        beforePublish.Version.IsTransitionInProgress.Should().BeTrue();
        beforePublish.MissingOwnerIds.Should().Equal(OWNER);
        afterPublish.MissingOwnerIds.Should().BeEmpty();
        afterPublish.Version.PublicationEpoch.Should().BeGreaterThan(beforePublish.Version.PublicationEpoch);
        afterPublish.Version.ChangeEpoch.Should().Be(0);
    }

    [Fact]
    public async Task PublishOwnerSnapshotAsync_WhenRevisionOrContentChanges_ShouldRejectTheConflict()
    {
        var store = CreateStore();
        await store.StageReleaseAsync(
            Stage("release-1", "revision-1", 1),
            TestContext.Current.CancellationToken);

        var wrongRevision = Snapshot("release-1", "revision-2", Declaration("Jobs.Daily"));
        await store.Invoking(candidate => candidate.PublishOwnerSnapshotAsync(
                wrongRevision,
                TestContext.Current.CancellationToken))
            .Should().ThrowAsync<JobCatalogConflictException>();

        var accepted = Snapshot("release-1", "revision-1", Declaration("Jobs.Daily"));
        await store.PublishOwnerSnapshotAsync(accepted, TestContext.Current.CancellationToken);
        var changed = Snapshot(
            "release-1",
            "revision-1",
            Declaration("Jobs.Daily") with { MaxConcurrency = 2 });

        await store.Invoking(candidate => candidate.PublishOwnerSnapshotAsync(
                changed,
                TestContext.Current.CancellationToken))
            .Should().ThrowAsync<JobCatalogConflictException>();
    }

    [Fact]
    public async Task StageReleaseAsync_WhenCallerMutatesInputCollection_ShouldRetainTheImmutableManifest()
    {
        var owners = new List<JobCatalogOwnerManifest> { new(OWNER, "revision-1") };
        var manifest = new JobCatalogReleaseManifest(SCOPE, "release-1", owners);
        var expectedHash = manifest.ContentHash;
        var store = CreateStore();
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(manifest, 1),
            TestContext.Current.CancellationToken);

        owners[0] = new JobCatalogOwnerManifest(OWNER, "mutated-revision");
        var duplicate = await store.StageReleaseAsync(
            Stage("release-1", "revision-1", 1),
            TestContext.Current.CancellationToken);

        duplicate.Status.Should().Be(ReleaseStageStatus.Duplicate);
        duplicate.ContentHash.Should().Be(expectedHash);
    }

    private static InMemoryJobSchedulerStore CreateStore() => new(TimeProvider.System);

    private static async Task PublishCompleteReleaseAsync(
        InMemoryJobSchedulerStore store,
        string releaseId,
        string revisionId,
        string jobKey,
        long deploymentGeneration,
        string ownerId = OWNER)
    {
        await store.StageReleaseAsync(
            Stage(releaseId, revisionId, deploymentGeneration, ownerId),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            SnapshotForOwner(releaseId, revisionId, ownerId, Declaration(jobKey)),
            TestContext.Current.CancellationToken);
    }

    private static JobCatalogReleaseStage Stage(
        string releaseId,
        string revisionId,
        long generation,
        string ownerId = OWNER) =>
        new(Manifest(releaseId, revisionId, ownerId), generation);

    private static JobCatalogReleaseManifest Manifest(
        string releaseId,
        string revisionId,
        string ownerId = OWNER) =>
        new(SCOPE, releaseId, [new JobCatalogOwnerManifest(ownerId, revisionId)]);

    private static JobOwnerCatalogSnapshot Snapshot(
        string releaseId,
        string revisionId,
        params JobDeclaration[] declarations) =>
        SnapshotForOwner(releaseId, revisionId, OWNER, declarations);

    private static JobOwnerCatalogSnapshot SnapshotForOwner(
        string releaseId,
        string revisionId,
        string ownerId,
        params JobDeclaration[] declarations) =>
        new(SCOPE, releaseId, ownerId, revisionId, declarations);

    private static JobDeclaration Declaration(string jobKey) => new()
    {
        JobKey = jobKey,
        JobName = jobKey,
        JobType = JobType.Recurring,
        CronExpression = "0 0 0 * * *",
        TimeZoneId = TimeZoneInfo.Utc.Id,
        MaxConcurrency = 1,
        RetryCount = 1,
        MaxExecutionTimeout = TimeSpan.FromMinutes(5)
    };
}
