using AwesomeAssertions;
using Monica.JobScheduler.Exceptions.Catalog;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
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
                Overrides = new JobPolicyOverrides
                {
                    DisabledOverride = true,
                    MaxRetainedHistoryRecords = 25,
                    MaxRetentionDays = 7
                },
                ExpectedConcurrencyStamp = active!.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        await PublishCompleteReleaseAsync(store, "release-2", "revision-2", "Jobs.Daily", 2);
        var second = await store.TryActivateReleaseAsync(SCOPE, "release-2", TestContext.Current.CancellationToken);
        var afterSecond = (await store.GetActiveDefinitionAsync(
            SCOPE,
            "Jobs.Daily",
            TestContext.Current.CancellationToken))!;
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
        AssertStickyPolicyState(afterSecond.Policy, changedPolicy);
        afterSecond.Policy.ConcurrencyStamp.Should().NotBe(changedPolicy.ConcurrencyStamp);
        AssertStickyPolicyState(restored!.Policy, changedPolicy);
        restored.Policy.ConcurrencyStamp.Should().NotBe(afterSecond.Policy.ConcurrencyStamp);
        restored.JobRevisionId.Should().Be(restored.CreateExecutionTemplate().Revision.JobRevisionId);
        restored.CreateExecutionTemplate().JobName.Should().Be(restored.Declaration.JobName);
    }

    [Fact]
    public async Task Activation_WhenJobRevisionChanges_ShouldRotateOnlyThePolicyFence()
    {
        var store = CreateStore();
        await PublishCompleteReleaseAsync(store, "release-1", "revision-1", "Jobs.Daily", 1);
        await store.TryActivateReleaseAsync(SCOPE, "release-1", TestContext.Current.CancellationToken);
        var revisionA = (await store.GetActiveDefinitionAsync(
            SCOPE,
            "Jobs.Daily",
            TestContext.Current.CancellationToken))!;
        var savedAgainstRevisionA = await store.UpdatePolicyAsync(
            SCOPE,
            OWNER,
            revisionA.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = revisionA.Policy.Overrides with
                {
                    DisplayNameOverride = "Operator title",
                    ScheduleOverride = new JobScheduleOverride { CronExpression = "0 */5 * * * *" }
                },
                ExpectedConcurrencyStamp = revisionA.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        await PublishCompleteReleaseAsync(store, "release-2", "revision-2", "Jobs.Daily", 2);
        await store.TryActivateReleaseAsync(SCOPE, "release-2", TestContext.Current.CancellationToken);
        var revisionB = (await store.GetActiveDefinitionAsync(
            SCOPE,
            "Jobs.Daily",
            TestContext.Current.CancellationToken))!;

        AssertStickyPolicyState(revisionB.Policy, savedAgainstRevisionA);
        revisionB.Policy.ConcurrencyStamp.Should().NotBe(savedAgainstRevisionA.ConcurrencyStamp);
        revisionB.CreateExecutionTemplate().AppliedPolicyRevision.Should().Be(revisionB.Policy.ConcurrencyStamp);
        await store.Invoking(candidate => candidate.UpdatePolicyAsync(
                SCOPE,
                OWNER,
                revisionB.Declaration.JobKey,
                new JobPolicyChange
                {
                    Overrides = revisionB.Policy.Overrides with
                    {
                        DescriptionOverride = new JobDescriptionOverride { Value = "stale edit" }
                    },
                    ExpectedConcurrencyStamp = savedAgainstRevisionA.ConcurrencyStamp
                },
                TestContext.Current.CancellationToken))
            .Should().ThrowAsync<JobPolicyConcurrencyException>();

        await PublishCompleteReleaseAsync(store, "release-3", "revision-2", "Jobs.Daily", 3);
        await store.TryActivateReleaseAsync(SCOPE, "release-3", TestContext.Current.CancellationToken);
        var sameRevision = (await store.GetActiveDefinitionAsync(
            SCOPE,
            "Jobs.Daily",
            TestContext.Current.CancellationToken))!;

        sameRevision.JobRevisionId.Should().Be(revisionB.JobRevisionId);
        sameRevision.Policy.ConcurrencyStamp.Should().Be(revisionB.Policy.ConcurrencyStamp);
        AssertStickyPolicyState(sameRevision.Policy, savedAgainstRevisionA);
    }

    [Fact]
    public async Task Activation_WhenStickyJobReturnsAfterAbsence_ShouldRotateThePolicyFence()
    {
        var store = CreateStore();
        await PublishCompleteReleaseAsync(store, "release-1", "revision-1", "Jobs.Daily", 1);
        await store.TryActivateReleaseAsync(SCOPE, "release-1", TestContext.Current.CancellationToken);
        var first = (await store.GetActiveDefinitionAsync(
            SCOPE,
            "Jobs.Daily",
            TestContext.Current.CancellationToken))!;
        var sticky = await store.UpdatePolicyAsync(
            SCOPE,
            OWNER,
            first.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = first.Policy.Overrides with { DisplayNameOverride = "Sticky title" },
                ExpectedConcurrencyStamp = first.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        await store.StageReleaseAsync(Stage("release-2", "revision-2", 2), TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            Snapshot("release-2", "revision-2"),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(SCOPE, "release-2", TestContext.Current.CancellationToken);
        await store.ReactivateReleaseAsync(SCOPE, "release-1", TestContext.Current.CancellationToken);
        var restored = (await store.GetActiveDefinitionAsync(
            SCOPE,
            "Jobs.Daily",
            TestContext.Current.CancellationToken))!;

        AssertStickyPolicyState(restored.Policy, sticky);
        restored.Policy.ConcurrencyStamp.Should().NotBe(sticky.ConcurrencyStamp);
    }

    [Fact]
    public async Task Policy_WhenEveryOverrideIsPersistedAndReset_ShouldMatchEffectiveProjection()
    {
        var store = CreateStore();
        var declaration = Declaration("Jobs.Daily") with
        {
            JobName = "Declared name",
            Description = "Declared description",
            CronExpression = "0 0 * * * *",
            StartTimeUtc = new DateTimeOffset(2026, 8, 18, 0, 0, 0, TimeSpan.Zero),
            EndTimeUtc = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
            MaxConcurrency = 2,
            RetryCount = 1,
            MaxExecutionTimeout = TimeSpan.FromMinutes(5)
        };
        await store.StageReleaseAsync(
            Stage("release-1", "revision-1", 1),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            Snapshot("release-1", "revision-1", declaration),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(SCOPE, "release-1", TestContext.Current.CancellationToken);
        var original = (await store.GetActiveDefinitionAsync(
            SCOPE,
            declaration.JobKey,
            TestContext.Current.CancellationToken))!;
        var replacementEnd = new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero);
        var overrides = new JobPolicyOverrides
        {
            DisabledOverride = true,
            DisplayNameOverride = "Operator name",
            DescriptionOverride = new JobDescriptionOverride { Value = null },
            MaxConcurrencyOverride = 4,
            RetryCountOverride = 3,
            MaxExecutionTimeoutOverride = TimeSpan.FromMinutes(12),
            ScheduleOverride = new JobScheduleOverride
            {
                CronExpression = "0 */5 * * * *",
                StartTimeUtc = new JobScheduleBoundaryOverride { Value = null },
                EndTimeUtc = new JobScheduleBoundaryOverride { Value = replacementEnd }
            },
            MaxRetainedHistoryRecords = 0,
            MaxRetentionDays = 30
        };

        var saved = await store.UpdatePolicyAsync(
            SCOPE,
            OWNER,
            declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = overrides,
                ExpectedConcurrencyStamp = original.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var projected = (await store.GetActiveDefinitionAsync(
            SCOPE,
            declaration.JobKey,
            TestContext.Current.CancellationToken))!;

        saved.Overrides.Should().Be(overrides);
        saved.ConcurrencyStamp.Should().NotBe(original.Policy.ConcurrencyStamp);
        saved.ReviewedAgainstJobRevisionId.Should().Be(projected.JobRevisionId);
        projected.Policy.Should().Be(saved);
        projected.IsPolicyReviewOutdated.Should().BeFalse();
        projected.EffectiveConfiguration.JobName.Should().Be("Operator name");
        projected.EffectiveConfiguration.Description.Should().BeNull();
        projected.EffectiveConfiguration.IsDisabled.Should().BeTrue();
        projected.EffectiveConfiguration.MaxConcurrency.Should().Be(4);
        projected.EffectiveConfiguration.RetryCount.Should().Be(3);
        projected.EffectiveConfiguration.MaxExecutionTimeout.Should().Be(TimeSpan.FromMinutes(12));
        projected.EffectiveConfiguration.MaxRetainedHistoryRecords.Should().Be(0);
        projected.EffectiveConfiguration.MaxRetentionDays.Should().Be(30);
        projected.EffectiveConfiguration.Schedule.Should().BeEquivalentTo(new RecurringScheduleDefinition
        {
            CronExpression = "0 */5 * * * *",
            TimeZoneId = declaration.TimeZoneId!,
            StartTimeUtc = null,
            EndTimeUtc = replacementEnd
        });

        var reset = await store.UpdatePolicyAsync(
            SCOPE,
            OWNER,
            declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = new JobPolicyOverrides(),
                ExpectedConcurrencyStamp = saved.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var resetProjection = (await store.GetActiveDefinitionAsync(
            SCOPE,
            declaration.JobKey,
            TestContext.Current.CancellationToken))!;

        reset.Overrides.HasAnyOverride.Should().BeFalse();
        reset.ConcurrencyStamp.Should().NotBe(saved.ConcurrencyStamp);
        reset.ReviewedAgainstJobRevisionId.Should().Be(resetProjection.JobRevisionId);
        resetProjection.Policy.Should().Be(reset);
        resetProjection.IsPolicyReviewOutdated.Should().BeFalse();
        resetProjection.EffectiveConfiguration.JobName.Should().Be(declaration.JobName);
        resetProjection.EffectiveConfiguration.Description.Should().Be(declaration.Description);
        resetProjection.EffectiveConfiguration.IsDisabled.Should().BeFalse();
        resetProjection.EffectiveConfiguration.MaxConcurrency.Should().Be(declaration.MaxConcurrency);
        resetProjection.EffectiveConfiguration.RetryCount.Should().Be(declaration.RetryCount);
        resetProjection.EffectiveConfiguration.MaxExecutionTimeout.Should().Be(declaration.MaxExecutionTimeout);
        resetProjection.EffectiveConfiguration.MaxRetainedHistoryRecords
            .Should().Be(JobPolicy.DEFAULT_MAX_RETAINED_HISTORY_RECORDS);
        resetProjection.EffectiveConfiguration.MaxRetentionDays.Should().BeNull();
        resetProjection.EffectiveConfiguration.Schedule.Should().BeEquivalentTo(new RecurringScheduleDefinition
        {
            CronExpression = declaration.CronExpression!,
            TimeZoneId = declaration.TimeZoneId!,
            StartTimeUtc = declaration.StartTimeUtc,
            EndTimeUtc = declaration.EndTimeUtc
        });
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
                Overrides = new JobPolicyOverrides
                {
                    DisabledOverride = true,
                    MaxRetainedHistoryRecords = 25
                },
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
        AssertStickyPolicyState(moved.Policy, firstPolicy);
        moved.Policy.ConcurrencyStamp.Should().NotBe(firstPolicy.ConcurrencyStamp);
        await store.Invoking(candidate => candidate.UpdatePolicyAsync(
                SCOPE,
                OWNER,
                "Jobs.Daily",
                new JobPolicyChange
                {
                    Overrides = moved.Policy.Overrides,
                    ExpectedConcurrencyStamp = moved.Policy.ConcurrencyStamp
                },
                TestContext.Current.CancellationToken))
            .Should().ThrowAsync<JobCatalogNotFoundException>();

        var replacementPolicy = await store.UpdatePolicyAsync(
            SCOPE,
            replacementOwner,
            "Jobs.Daily",
            new JobPolicyChange
            {
                Overrides = new JobPolicyOverrides
                {
                    DisabledOverride = false,
                    MaxRetainedHistoryRecords = 12
                },
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
        AssertStickyPolicyState(restored.Policy, replacementPolicy);
        restored.Policy.ConcurrencyStamp.Should().NotBe(replacementPolicy.ConcurrencyStamp);
    }

    [Fact]
    public async Task Policy_WhenRecurringJobBecomesTriggeredUnderANewOwner_ShouldKeepDormantScheduleAndAllowUnrelatedEdits()
    {
        var store = CreateStore();
        await PublishCompleteReleaseAsync(store, "release-1", "revision-1", "Jobs.Daily", 1);
        await store.TryActivateReleaseAsync(SCOPE, "release-1", TestContext.Current.CancellationToken);
        var recurring = (await store.GetActiveDefinitionAsync(
            SCOPE,
            "Jobs.Daily",
            TestContext.Current.CancellationToken))!;
        var recurringPolicy = await store.UpdatePolicyAsync(
            SCOPE,
            OWNER,
            recurring.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = recurring.Policy.Overrides with
                {
                    ScheduleOverride = new JobScheduleOverride { CronExpression = "0 */5 * * * *" }
                },
                ExpectedConcurrencyStamp = recurring.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        const string replacementOwner = "worker-b";
        await store.StageReleaseAsync(
            Stage("release-2", "revision-2", 2, replacementOwner),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            SnapshotForOwner(
                "release-2",
                "revision-2",
                replacementOwner,
                new JobDeclaration
                {
                    JobKey = recurring.Declaration.JobKey,
                    JobArgsKey = "Jobs.DailyArgs",
                    JobName = "Triggered daily",
                    JobType = JobType.Triggered,
                    MaxConcurrency = 2,
                    MaxExecutionTimeout = TimeSpan.FromMinutes(5)
                }),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(SCOPE, "release-2", TestContext.Current.CancellationToken);
        var triggered = (await store.GetActiveDefinitionAsync(
            SCOPE,
            recurring.Declaration.JobKey,
            TestContext.Current.CancellationToken))!;

        var renamedPolicy = await store.UpdatePolicyAsync(
            SCOPE,
            replacementOwner,
            triggered.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = triggered.Policy.Overrides with { DisplayNameOverride = "Operator daily" },
                ExpectedConcurrencyStamp = triggered.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var scheduleChange = async () => await store.UpdatePolicyAsync(
            SCOPE,
            replacementOwner,
            triggered.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = renamedPolicy.Overrides with
                {
                    ScheduleOverride = new JobScheduleOverride { CronExpression = "0 */10 * * * *" }
                },
                ExpectedConcurrencyStamp = renamedPolicy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        await scheduleChange.Should().ThrowAsync<ArgumentException>();
        const string returningOwner = "worker-c";
        await store.StageReleaseAsync(
            Stage("release-3", "revision-3", 3, returningOwner),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            SnapshotForOwner(
                "release-3",
                "revision-3",
                returningOwner,
                new JobDeclaration
                {
                    JobKey = recurring.Declaration.JobKey,
                    JobName = "Recurring daily again",
                    JobType = JobType.Recurring,
                    CronExpression = "0 0 * * * *",
                    TimeZoneId = TimeZoneInfo.Utc.Id,
                    MaxConcurrency = 3,
                    MaxExecutionTimeout = TimeSpan.FromMinutes(5)
                }),
            TestContext.Current.CancellationToken);
        await store.TryActivateReleaseAsync(SCOPE, "release-3", TestContext.Current.CancellationToken);
        var returningRecurring = (await store.GetActiveDefinitionAsync(
            SCOPE,
            recurring.Declaration.JobKey,
            TestContext.Current.CancellationToken))!;
        var resetPolicy = await store.UpdatePolicyAsync(
            SCOPE,
            returningOwner,
            returningRecurring.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = returningRecurring.Policy.Overrides with { ScheduleOverride = null },
                ExpectedConcurrencyStamp = returningRecurring.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        triggered.OwnerId.Should().Be(replacementOwner);
        AssertStickyPolicyState(triggered.Policy, recurringPolicy);
        triggered.Policy.ConcurrencyStamp.Should().NotBe(recurringPolicy.ConcurrencyStamp);
        triggered.IsPolicyReviewOutdated.Should().BeTrue();
        triggered.EffectiveConfiguration.Schedule.Should().BeNull();
        renamedPolicy.Overrides.ScheduleOverride.Should().Be(recurringPolicy.Overrides.ScheduleOverride);
        returningRecurring.OwnerId.Should().Be(returningOwner);
        AssertStickyPolicyState(returningRecurring.Policy, renamedPolicy);
        returningRecurring.Policy.ConcurrencyStamp.Should().NotBe(renamedPolicy.ConcurrencyStamp);
        returningRecurring.IsPolicyReviewOutdated.Should().BeTrue();
        returningRecurring.EffectiveConfiguration.JobName.Should().Be("Operator daily");
        returningRecurring.EffectiveConfiguration.Schedule!.CronExpression.Should().Be("0 */5 * * * *");
        resetPolicy.Overrides.ScheduleOverride.Should().BeNull();
        resetPolicy.ReviewedAgainstJobRevisionId.Should().Be(returningRecurring.JobRevisionId);
    }

    [Fact]
    public async Task Activation_WhenStickyScheduleConflictsWithChangedDeclarationBoundary_ShouldRejectAtomically()
    {
        var store = CreateStore();
        await PublishCompleteReleaseAsync(store, "release-1", "revision-1", "Jobs.Daily", 1);
        await store.TryActivateReleaseAsync(SCOPE, "release-1", TestContext.Current.CancellationToken);
        var current = (await store.GetActiveDefinitionAsync(
            SCOPE,
            "Jobs.Daily",
            TestContext.Current.CancellationToken))!;
        var stickyPolicy = await store.UpdatePolicyAsync(
            SCOPE,
            OWNER,
            current.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = current.Policy.Overrides with
                {
                    ScheduleOverride = new JobScheduleOverride
                    {
                        StartTimeUtc = new JobScheduleBoundaryOverride
                        {
                            Value = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero)
                        }
                    }
                },
                ExpectedConcurrencyStamp = current.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var beforeStage = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);

        await store.StageReleaseAsync(
            Stage("release-2", "revision-2", 2),
            TestContext.Current.CancellationToken);
        await store.PublishOwnerSnapshotAsync(
            Snapshot(
                "release-2",
                "revision-2",
                Declaration("Jobs.Daily") with
                {
                    EndTimeUtc = new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero)
                }),
            TestContext.Current.CancellationToken);

        var activation = async () => await store.TryActivateReleaseAsync(
            SCOPE,
            "release-2",
            TestContext.Current.CancellationToken);
        await activation.Should()
            .ThrowAsync<JobCatalogConflictException>()
            .WithMessage("*Jobs.Daily*");
        var afterFailure = await store.GetCatalogVersionAsync(SCOPE, TestContext.Current.CancellationToken);
        var stillActive = (await store.GetActiveDefinitionAsync(
            SCOPE,
            "Jobs.Daily",
            TestContext.Current.CancellationToken))!;

        afterFailure.ActiveReleaseId.Should().Be("release-1");
        afterFailure.ActivationEpoch.Should().Be(beforeStage.ActivationEpoch);
        afterFailure.ChangeEpoch.Should().Be(beforeStage.ChangeEpoch);
        afterFailure.DesiredReleaseId.Should().Be("release-2");
        afterFailure.IsTransitionInProgress.Should().BeTrue();
        stillActive.ReleaseId.Should().Be("release-1");
        stillActive.Policy.Should().Be(stickyPolicy);
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

    private static void AssertStickyPolicyState(JobPolicy actual, JobPolicy expected)
    {
        actual.Overrides.Should().Be(expected.Overrides);
        actual.ReviewedAgainstJobRevisionId.Should().Be(expected.ReviewedAgainstJobRevisionId);
        actual.RecurringScheduleEffectiveFromUtc.Should().Be(expected.RecurringScheduleEffectiveFromUtc);
        actual.UpdatedAtUtc.Should().Be(expected.UpdatedAtUtc);
    }
}
