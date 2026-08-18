using AwesomeAssertions;
using Monica.JobScheduler.Exceptions.Catalog;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Xunit;

namespace Test.Monica.JobScheduler.Stores.EfCore;

public sealed partial class EfCoreJobSchedulerStoreTests
{
    [Fact]
    public async Task Activation_WhenJobRevisionChanges_ShouldFenceStalePolicyEditorsWithoutMutatingPolicyState()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        await ActivateDefinitionAsync(fixture, CreateRecurringDeclaration("job-a"));
        var revisionA = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var savedAgainstRevisionA = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            revisionA.OwnerId,
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

        await fixture.Store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2"), 2),
            TestContext.Current.CancellationToken);
        await fixture.Store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                fixture.Scope,
                "release-2",
                "worker-a",
                "revision-2",
                [CreateRecurringDeclaration("job-a")]),
            TestContext.Current.CancellationToken);
        await fixture.Store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);
        var revisionB = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();

        AssertStickyPolicyState(revisionB.Policy, savedAgainstRevisionA);
        revisionB.Policy.ConcurrencyStamp.Should().NotBe(savedAgainstRevisionA.ConcurrencyStamp);
        revisionB.CreateExecutionTemplate().AppliedPolicyRevision.Should().Be(revisionB.Policy.ConcurrencyStamp);
        await fixture.SecondStore.Invoking(store => store.UpdatePolicyAsync(
                fixture.Scope,
                revisionB.OwnerId,
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

        await fixture.Store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-3", "revision-2"), 3),
            TestContext.Current.CancellationToken);
        await fixture.Store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                fixture.Scope,
                "release-3",
                "worker-a",
                "revision-2",
                [CreateRecurringDeclaration("job-a")]),
            TestContext.Current.CancellationToken);
        await fixture.Store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-3",
            TestContext.Current.CancellationToken);
        var sameRevision = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();

        sameRevision.JobRevisionId.Should().Be(revisionB.JobRevisionId);
        sameRevision.Policy.ConcurrencyStamp.Should().Be(revisionB.Policy.ConcurrencyStamp);
        AssertStickyPolicyState(sameRevision.Policy, savedAgainstRevisionA);
    }

    [Fact]
    public async Task Activation_WhenStickyJobReturnsAfterAbsence_ShouldRotateThePolicyFence()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        await ActivateDefinitionAsync(fixture, CreateTriggeredDeclaration("job-a"));
        var first = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var sticky = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            first.OwnerId,
            first.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = first.Policy.Overrides with { DisplayNameOverride = "Sticky title" },
                ExpectedConcurrencyStamp = first.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        await fixture.Store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2"), 2),
            TestContext.Current.CancellationToken);
        await fixture.Store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(fixture.Scope, "release-2", "worker-a", "revision-2", []),
            TestContext.Current.CancellationToken);
        await fixture.Store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);
        await fixture.Store.ReactivateReleaseAsync(
            fixture.Scope,
            "release-1",
            TestContext.Current.CancellationToken);
        var restored = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();

        AssertStickyPolicyState(restored.Policy, sticky);
        restored.Policy.ConcurrencyStamp.Should().NotBe(sticky.ConcurrencyStamp);
    }

    [Fact]
    public async Task Activation_WhenRichPolicyJobMovesOwners_ShouldPreserveOverridesAndExposeReviewDrift()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        var declarationA = CreateRecurringDeclaration("job-a") with
        {
            JobName = "Declared A",
            Description = "Declared description A",
            CronExpression = "0 0 * * * *",
            StartTimeUtc = START_TIME.AddHours(1),
            EndTimeUtc = START_TIME.AddDays(2),
            MaxConcurrency = 2,
            RetryCount = 1,
            MaxExecutionTimeout = TimeSpan.FromMinutes(5)
        };
        await ActivateDefinitionAsync(fixture, declarationA);
        var first = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var replacementEnd = START_TIME.AddDays(5);
        var overrides = CreateRichPolicyOverrides(replacementEnd);
        var savedAgainstA = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            first.OwnerId,
            first.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = overrides,
                ExpectedConcurrencyStamp = first.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        const string replacementOwner = "worker-b";
        var declarationB = CreateRecurringDeclaration("job-a") with
        {
            JobName = "Declared B",
            Description = "Declared description B",
            CronExpression = "0 30 * * * *",
            StartTimeUtc = START_TIME.AddHours(2),
            EndTimeUtc = START_TIME.AddDays(3),
            MaxConcurrency = 6,
            RetryCount = 5,
            MaxExecutionTimeout = TimeSpan.FromMinutes(20)
        };
        await fixture.Store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2", replacementOwner), 2),
            TestContext.Current.CancellationToken);
        await fixture.Store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                fixture.Scope,
                "release-2",
                replacementOwner,
                "revision-2",
                [declarationB]),
            TestContext.Current.CancellationToken);
        await fixture.Store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);
        var moved = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();

        moved.OwnerId.Should().Be(replacementOwner);
        moved.JobRevisionId.Should().NotBe(first.JobRevisionId);
        AssertStickyPolicyState(moved.Policy, savedAgainstA);
        moved.Policy.ConcurrencyStamp.Should().NotBe(savedAgainstA.ConcurrencyStamp);
        moved.Policy.ReviewedAgainstJobRevisionId.Should().Be(first.JobRevisionId);
        moved.IsPolicyReviewOutdated.Should().BeTrue();
        AssertRichEffectiveConfiguration(moved.EffectiveConfiguration, declarationB.TimeZoneId!, replacementEnd);
        moved.CreateExecutionTemplate().AppliedPolicyRevision.Should().Be(moved.Policy.ConcurrencyStamp);
        await fixture.SecondStore.Invoking(store => store.UpdatePolicyAsync(
                fixture.Scope,
                first.OwnerId,
                first.Declaration.JobKey,
                new JobPolicyChange
                {
                    Overrides = moved.Policy.Overrides,
                    ExpectedConcurrencyStamp = moved.Policy.ConcurrencyStamp
                },
                TestContext.Current.CancellationToken))
            .Should().ThrowAsync<JobCatalogNotFoundException>();
    }

    [Fact]
    public async Task ReactivateReleaseAsync_WhenRollingBackFromOwnerBToOwnerA_ShouldKeepLatestRichPolicyAndRotateFence()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        var declarationA = CreateRecurringDeclaration("job-a") with
        {
            JobName = "Declared A",
            Description = "Declared description A",
            CronExpression = "0 0 * * * *",
            StartTimeUtc = START_TIME.AddHours(1),
            EndTimeUtc = START_TIME.AddDays(2),
            MaxConcurrency = 2,
            RetryCount = 1,
            MaxExecutionTimeout = TimeSpan.FromMinutes(5)
        };
        await ActivateDefinitionAsync(fixture, declarationA);
        var first = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();

        const string replacementOwner = "worker-b";
        var declarationB = declarationA with
        {
            JobName = "Declared B",
            Description = "Declared description B",
            CronExpression = "0 30 * * * *",
            MaxConcurrency = 6,
            RetryCount = 5,
            MaxExecutionTimeout = TimeSpan.FromMinutes(20)
        };
        await fixture.Store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2", replacementOwner), 2),
            TestContext.Current.CancellationToken);
        await fixture.Store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                fixture.Scope,
                "release-2",
                replacementOwner,
                "revision-2",
                [declarationB]),
            TestContext.Current.CancellationToken);
        await fixture.Store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);
        var moved = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var replacementEnd = START_TIME.AddDays(5);
        var savedAgainstB = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            replacementOwner,
            moved.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = CreateRichPolicyOverrides(replacementEnd),
                ExpectedConcurrencyStamp = moved.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var reviewedB = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();

        reviewedB.IsPolicyReviewOutdated.Should().BeFalse();
        savedAgainstB.ReviewedAgainstJobRevisionId.Should().Be(moved.JobRevisionId);
        var rollback = await fixture.Store.ReactivateReleaseAsync(
            fixture.Scope,
            "release-1",
            TestContext.Current.CancellationToken);
        var restored = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();

        rollback.ReleaseId.Should().Be("release-1");
        restored.ReleaseId.Should().Be("release-1");
        restored.OwnerId.Should().Be(first.OwnerId);
        restored.JobRevisionId.Should().Be(first.JobRevisionId);
        AssertStickyPolicyState(restored.Policy, savedAgainstB);
        restored.Policy.ConcurrencyStamp.Should().NotBe(savedAgainstB.ConcurrencyStamp);
        restored.Policy.ReviewedAgainstJobRevisionId.Should().Be(moved.JobRevisionId);
        restored.IsPolicyReviewOutdated.Should().BeTrue();
        AssertRichEffectiveConfiguration(restored.EffectiveConfiguration, declarationA.TimeZoneId!, replacementEnd);
        restored.CreateExecutionTemplate().AppliedPolicyRevision.Should().Be(restored.Policy.ConcurrencyStamp);
    }

    [Fact]
    public async Task Policy_WhenEveryOverrideIsReplacedAndReset_ShouldProjectEffectiveValues()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        var declaration = new JobDeclaration
        {
            JobKey = "job-a",
            JobName = "Declared name",
            Description = "Declared description",
            JobType = JobType.Recurring,
            CronExpression = "0 * * * * *",
            TimeZoneId = TimeZoneInfo.Utc.Id,
            StartTimeUtc = START_TIME.AddHours(1),
            EndTimeUtc = START_TIME.AddDays(2),
            MaxConcurrency = 2,
            RetryCount = 1,
            MaxExecutionTimeout = TimeSpan.FromMinutes(5)
        };
        await ActivateDefinitionAsync(fixture, declaration);
        var original = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var replacementEnd = START_TIME.AddDays(3);
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

        var replaced = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            original.OwnerId,
            declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = overrides,
                ExpectedConcurrencyStamp = original.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var projected = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();

        replaced.Overrides.Should().Be(overrides);
        replaced.ReviewedAgainstJobRevisionId.Should().Be(projected.JobRevisionId);
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

        await fixture.SecondStore.Invoking(store => store.UpdatePolicyAsync(
                fixture.Scope,
                original.OwnerId,
                declaration.JobKey,
                new JobPolicyChange
                {
                    Overrides = new JobPolicyOverrides(),
                    ExpectedConcurrencyStamp = original.Policy.ConcurrencyStamp
                },
                TestContext.Current.CancellationToken))
            .Should().ThrowAsync<JobPolicyConcurrencyException>();

        var reset = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            original.OwnerId,
            declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = new JobPolicyOverrides(),
                ExpectedConcurrencyStamp = replaced.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var resetProjection = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();

        reset.Overrides.HasAnyOverride.Should().BeFalse();
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
    public async Task PolicySchedule_WhenSaved_ShouldFenceOldOccurrenceAndResumeWithoutBackfill()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        await ActivateDefinitionAsync(fixture, CreateRecurringDeclaration("job-a"));
        var catalog = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!;
        var definition = catalog.Definitions.Single();
        var initial = await fixture.Store.SynchronizeRecurringScheduleAsync(
            CreateSynchronization(definition, catalog.Version.ChangeEpoch),
            TestContext.Current.CancellationToken);
        var staleCursor = initial.Cursor!;
        staleCursor.NextOccurrenceUtc.Should().Be(START_TIME.AddMinutes(1));
        fixture.TimeProvider.Advance(TimeSpan.FromSeconds(30));

        var scheduledPolicy = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            definition.OwnerId,
            definition.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = definition.Policy.Overrides with
                {
                    ScheduleOverride = new JobScheduleOverride { CronExpression = "0 */5 * * * *" }
                },
                ExpectedConcurrencyStamp = definition.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var scheduledCatalog = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!;
        var scheduledDefinition = scheduledCatalog.Definitions.Single();
        var scheduledSync = await fixture.SecondStore.SynchronizeRecurringScheduleAsync(
            CreateSynchronization(scheduledDefinition, scheduledCatalog.Version.ChangeEpoch),
            TestContext.Current.CancellationToken);

        scheduledSync.Status.Should().Be(RecurringScheduleSynchronizationStatus.Unchanged);
        scheduledSync.Cursor!.AppliedPolicyRevision.Should().Be(scheduledPolicy.ConcurrencyStamp);
        scheduledSync.Cursor.Template.AppliedPolicyRevision.Should().Be(scheduledPolicy.ConcurrencyStamp);
        scheduledSync.Cursor.NextOccurrenceUtc.Should().Be(START_TIME.AddMinutes(5));
        scheduledSync.Cursor.Version.Should().Be(staleCursor.Version + 1);

        fixture.TimeProvider.Advance(TimeSpan.FromSeconds(30));
        var staleMaterialization = await fixture.Store.TryMaterializeRecurringOccurrenceAsync(
            new RecurringOccurrenceMaterialization
            {
                CursorKey = staleCursor.Key,
                ExpectedVersion = staleCursor.Version,
                ExpectedOccurrenceUtc = staleCursor.NextOccurrenceUtc!.Value,
                NextOccurrenceUtc = staleCursor.NextOccurrenceUtc.Value.AddMinutes(1),
                InstanceId = "old-cadence"
            },
            TestContext.Current.CancellationToken);
        staleMaterialization.Status.Should().Be(RecurringMaterializationStatus.StaleCursor);
        (await fixture.Store.QueryExecutionsAsync(new JobExecutionQuery
        {
            SchedulerScopeKey = fixture.Scope
        }, TestContext.Current.CancellationToken)).TotalCount.Should().Be(0);

        var executionPolicy = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            scheduledDefinition.OwnerId,
            scheduledDefinition.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = scheduledPolicy.Overrides with { MaxConcurrencyOverride = 2 },
                ExpectedConcurrencyStamp = scheduledPolicy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var executionCatalog = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!;
        var executionSync = await fixture.SecondStore.SynchronizeRecurringScheduleAsync(
            CreateSynchronization(executionCatalog.Definitions.Single(), executionCatalog.Version.ChangeEpoch),
            TestContext.Current.CancellationToken);
        executionSync.Cursor!.NextOccurrenceUtc.Should().Be(START_TIME.AddMinutes(5));
        executionSync.Cursor.AppliedPolicyRevision.Should().Be(executionPolicy.ConcurrencyStamp);

        var pausedPolicy = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            scheduledDefinition.OwnerId,
            scheduledDefinition.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = executionPolicy.Overrides with { DisabledOverride = true },
                ExpectedConcurrencyStamp = executionPolicy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var pausedCatalog = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!;
        var pausedSync = await fixture.SecondStore.SynchronizeRecurringScheduleAsync(
            CreateSynchronization(pausedCatalog.Definitions.Single(), pausedCatalog.Version.ChangeEpoch),
            TestContext.Current.CancellationToken);
        pausedSync.Cursor!.IsSuspended.Should().BeTrue();
        pausedSync.Cursor.NextOccurrenceUtc.Should().BeNull();

        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(10));
        var resumedPolicy = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            scheduledDefinition.OwnerId,
            scheduledDefinition.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = pausedPolicy.Overrides with { DisabledOverride = false },
                ExpectedConcurrencyStamp = pausedPolicy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var resumedCatalog = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!;
        var resumedSync = await fixture.SecondStore.SynchronizeRecurringScheduleAsync(
            CreateSynchronization(resumedCatalog.Definitions.Single(), resumedCatalog.Version.ChangeEpoch),
            TestContext.Current.CancellationToken);

        resumedSync.Cursor!.IsSuspended.Should().BeFalse();
        resumedSync.Cursor.AppliedPolicyRevision.Should().Be(resumedPolicy.ConcurrencyStamp);
        resumedSync.Cursor.NextOccurrenceUtc.Should().Be(START_TIME.AddMinutes(15));
    }

    [Fact]
    public async Task PolicySchedule_WhenSavedBeforeFirstSynchronization_ShouldStartAfterEffectiveFromWatermark()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        await ActivateDefinitionAsync(fixture, CreateRecurringDeclaration("job-a"));
        var definition = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(10));

        var updatedPolicy = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            definition.OwnerId,
            definition.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = definition.Policy.Overrides with
                {
                    ScheduleOverride = new JobScheduleOverride { CronExpression = "0 */5 * * * *" }
                },
                ExpectedConcurrencyStamp = definition.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var updatedCatalog = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!;
        var synchronized = await fixture.SecondStore.SynchronizeRecurringScheduleAsync(
            CreateSynchronization(updatedCatalog.Definitions.Single(), updatedCatalog.Version.ChangeEpoch),
            TestContext.Current.CancellationToken);

        updatedPolicy.RecurringScheduleEffectiveFromUtc.Should().Be(START_TIME.AddMinutes(10));
        synchronized.Status.Should().Be(RecurringScheduleSynchronizationStatus.Created);
        synchronized.Cursor!.NextOccurrenceUtc.Should().Be(START_TIME.AddMinutes(15));
        synchronized.Cursor.AppliedPolicyRevision.Should().Be(updatedPolicy.ConcurrencyStamp);
    }

    [Fact]
    public async Task Claim_WhenConcurrencyPolicyChanges_ShouldUseCurrentLimitAndPreserveExecutionSnapshots()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        var declaration = new JobDeclaration
        {
            JobKey = "job-a",
            JobArgsKey = "JobAArgs",
            JobName = "Declared name",
            JobType = JobType.Triggered,
            MaxConcurrency = 2,
            RetryCount = 1,
            MaxExecutionTimeout = TimeSpan.FromMinutes(2)
        };
        await ActivateDefinitionAsync(fixture, declaration);
        var definition = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        await EnqueueAsync(fixture.Store, fixture.Scope, definition, "queued-before-1");
        await EnqueueAsync(fixture.Store, fixture.Scope, definition, "queued-before-2");
        var constrained = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            definition.OwnerId,
            declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = definition.Policy.Overrides with { MaxConcurrencyOverride = 1 },
                ExpectedConcurrencyStamp = definition.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var capability = await fixture.Store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = definition.OwnerId,
            WorkerRevisionId = definition.WorkerRevisionId,
            WorkerInstanceId = "policy-worker",
            JobRevisionIds = [definition.JobRevisionId]
        }, TimeSpan.FromHours(1), TestContext.Current.CancellationToken);

        var firstClaims = await fixture.Store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = capability.LeaseKey,
            LeaseDuration = TimeSpan.FromMinutes(5),
            MaxCount = 2
        }, TestContext.Current.CancellationToken);
        firstClaims.Should().ContainSingle();
        firstClaims[0].Execution.Template.MaxConcurrency.Should().Be(2);

        var expanded = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            definition.OwnerId,
            declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = constrained.Overrides with
                {
                    DisplayNameOverride = "Operator name",
                    MaxConcurrencyOverride = 3,
                    RetryCountOverride = 4,
                    MaxExecutionTimeoutOverride = TimeSpan.FromMinutes(7)
                },
                ExpectedConcurrencyStamp = constrained.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var secondClaims = await fixture.Store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = capability.LeaseKey,
            LeaseDuration = TimeSpan.FromMinutes(5),
            MaxCount = 2
        }, TestContext.Current.CancellationToken);
        secondClaims.Should().ContainSingle();
        secondClaims[0].Execution.Template.MaxConcurrency.Should().Be(2);
        secondClaims[0].Execution.Template.RetryCount.Should().Be(1);
        secondClaims[0].Execution.Template.MaxExecutionTimeout.Should().Be(TimeSpan.FromMinutes(2));

        var currentDefinition = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var admittedAfterEdit = await EnqueueAsync(
            fixture.Store,
            fixture.Scope,
            currentDefinition,
            "queued-after");
        admittedAfterEdit.Template.AppliedPolicyRevision.Should().Be(expanded.ConcurrencyStamp);
        admittedAfterEdit.Template.JobName.Should().Be("Operator name");
        admittedAfterEdit.Template.MaxConcurrency.Should().Be(3);
        admittedAfterEdit.Template.RetryCount.Should().Be(4);
        admittedAfterEdit.Template.MaxExecutionTimeout.Should().Be(TimeSpan.FromMinutes(7));
    }

    [Fact]
    public async Task Activation_WhenConcurrencyChanges_ShouldRefreshGateWithoutResettingActiveCount()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        await ActivateDefinitionAsync(fixture, CreateTriggeredDeclaration("job-a"));
        var firstDefinition = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        await EnqueueAsync(fixture.Store, fixture.Scope, firstDefinition, "old-running");
        var firstCapability = await fixture.Store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = firstDefinition.OwnerId,
            WorkerRevisionId = firstDefinition.WorkerRevisionId,
            WorkerInstanceId = "old-worker",
            JobRevisionIds = [firstDefinition.JobRevisionId]
        }, TimeSpan.FromHours(1), TestContext.Current.CancellationToken);
        _ = await fixture.Store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = firstCapability.LeaseKey,
            LeaseDuration = TimeSpan.FromHours(1),
            MaxCount = 1
        }, TestContext.Current.CancellationToken);

        await fixture.Store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2"), 2),
            TestContext.Current.CancellationToken);
        await fixture.Store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                fixture.Scope,
                "release-2",
                "worker-a",
                "revision-2",
                [CreateTriggeredDeclaration("job-a") with { MaxConcurrency = 2 }]),
            TestContext.Current.CancellationToken);
        await fixture.Store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);
        var replacement = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        await EnqueueAsync(fixture.Store, fixture.Scope, replacement, "replacement-1");
        await EnqueueAsync(fixture.Store, fixture.Scope, replacement, "replacement-2");
        var replacementCapability = await fixture.Store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = replacement.OwnerId,
            WorkerRevisionId = replacement.WorkerRevisionId,
            WorkerInstanceId = "replacement-worker",
            JobRevisionIds = [replacement.JobRevisionId]
        }, TimeSpan.FromHours(1), TestContext.Current.CancellationToken);

        var replacementClaims = await fixture.Store.ClaimAsync(new JobClaimRequest
        {
            CapabilityLeaseKey = replacementCapability.LeaseKey,
            LeaseDuration = TimeSpan.FromHours(1),
            MaxCount = 2
        }, TestContext.Current.CancellationToken);

        replacementClaims.Should().ContainSingle();
        replacementClaims[0].Execution.Template.MaxConcurrency.Should().Be(2);
        var remaining = await fixture.Store.GetExecutionAsync(
            fixture.Scope,
            "replacement-2",
            TestContext.Current.CancellationToken);
        remaining!.State.Should().Be(JobExecutionState.Queued);
    }

    [Fact]
    public async Task CatalogSearch_WhenDisplayMetadataIsOverridden_ShouldUseEffectiveValues()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        await ActivateDefinitionAsync(fixture, new JobDeclaration
        {
            JobKey = "job-a",
            JobArgsKey = "JobAArgs",
            JobName = "Declared name",
            Description = "Declared description",
            JobType = JobType.Triggered
        });
        var definition = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        _ = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            definition.OwnerId,
            definition.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = definition.Policy.Overrides with
                {
                    DisplayNameOverride = "Operator title",
                    DescriptionOverride = new JobDescriptionOverride { Value = "Policy details" }
                },
                ExpectedConcurrencyStamp = definition.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        var byName = await fixture.Store.QueryActiveDefinitionsAsync(
            fixture.Scope,
            new JobCatalogQuery { SearchText = "operator" },
            TestContext.Current.CancellationToken);
        var byDescription = await fixture.Store.QueryOperationalSummariesAsync(
            fixture.Scope,
            new JobCatalogQuery { SearchText = "policy details" },
            TestContext.Current.CancellationToken);
        var oldName = await fixture.Store.QueryActiveDefinitionsAsync(
            fixture.Scope,
            new JobCatalogQuery { SearchText = "declared name" },
            TestContext.Current.CancellationToken);

        byName.Items.Should().ContainSingle();
        byDescription.Items.Should().ContainSingle();
        oldName.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Policy_WhenRecurringJobBecomesTriggered_ShouldKeepDormantScheduleUntilReset()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        await ActivateDefinitionAsync(fixture, CreateRecurringDeclaration("job-a"));
        var recurring = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var recurringPolicy = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            recurring.OwnerId,
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

        await fixture.Store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2"), 2),
            TestContext.Current.CancellationToken);
        await fixture.Store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                fixture.Scope,
                "release-2",
                "worker-a",
                "revision-2",
                [CreateTriggeredDeclaration("job-a")]),
            TestContext.Current.CancellationToken);
        await fixture.Store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);
        var triggered = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();

        AssertStickyPolicyState(triggered.Policy, recurringPolicy);
        triggered.Policy.ConcurrencyStamp.Should().NotBe(recurringPolicy.ConcurrencyStamp);
        triggered.IsPolicyReviewOutdated.Should().BeTrue();
        triggered.EffectiveConfiguration.Schedule.Should().BeNull();
        var metadataPolicy = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            triggered.OwnerId,
            triggered.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = triggered.Policy.Overrides with { DisplayNameOverride = "Triggered title" },
                ExpectedConcurrencyStamp = triggered.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        metadataPolicy.Overrides.ScheduleOverride.Should().Be(recurringPolicy.Overrides.ScheduleOverride);

        await fixture.Store.Invoking(store => store.UpdatePolicyAsync(
                fixture.Scope,
                triggered.OwnerId,
                triggered.Declaration.JobKey,
                new JobPolicyChange
                {
                    Overrides = metadataPolicy.Overrides with
                    {
                        ScheduleOverride = new JobScheduleOverride { CronExpression = "0 */10 * * * *" }
                    },
                    ExpectedConcurrencyStamp = metadataPolicy.ConcurrencyStamp
                },
                TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>();

        var resetPolicy = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            triggered.OwnerId,
            triggered.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = metadataPolicy.Overrides with { ScheduleOverride = null },
                ExpectedConcurrencyStamp = metadataPolicy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        resetPolicy.Overrides.ScheduleOverride.Should().BeNull();
    }

    [Fact]
    public async Task Activation_WhenStickyBoundaryConflictsWithIncomingDeclaration_ShouldRejectBeforeCutover()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        await ActivateDefinitionAsync(fixture, CreateRecurringDeclaration("job-a") with
        {
            EndTimeUtc = START_TIME.AddHours(6)
        });
        var firstCatalog = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!;
        var firstDefinition = firstCatalog.Definitions.Single();
        var stickyPolicy = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            firstDefinition.OwnerId,
            firstDefinition.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = firstDefinition.Policy.Overrides with
                {
                    ScheduleOverride = new JobScheduleOverride
                    {
                        StartTimeUtc = new JobScheduleBoundaryOverride
                        {
                            Value = START_TIME.AddHours(4)
                        }
                    }
                },
                ExpectedConcurrencyStamp = firstDefinition.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var beforeAttempt = await fixture.Store.GetCatalogVersionAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken);
        await fixture.Store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2"), 2),
            TestContext.Current.CancellationToken);
        await fixture.Store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                fixture.Scope,
                "release-2",
                "worker-a",
                "revision-2",
                [CreateRecurringDeclaration("job-a") with { EndTimeUtc = START_TIME.AddHours(3) }]),
            TestContext.Current.CancellationToken);

        var activation = async () => await fixture.Store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);

        (await activation.Should().ThrowAsync<JobCatalogConflictException>())
            .Which.Message.Should().Contain("job-a").And.Contain("release-2");
        var afterAttempt = await fixture.Store.GetCatalogVersionAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken);
        var stillActive = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        afterAttempt.ActiveReleaseId.Should().Be("release-1");
        afterAttempt.ActivationEpoch.Should().Be(beforeAttempt.ActivationEpoch);
        stillActive.Policy.Should().Be(stickyPolicy);
    }

    [Fact]
    public async Task Activation_WhenRecurringBecomesTriggeredAndRecurringAgain_ShouldRestoreDormantScheduleOverride()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        await ActivateDefinitionAsync(fixture, CreateRecurringDeclaration("job-a"));
        var firstDefinition = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();
        var policy = await fixture.Store.UpdatePolicyAsync(
            fixture.Scope,
            firstDefinition.OwnerId,
            firstDefinition.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = firstDefinition.Policy.Overrides with
                {
                    ScheduleOverride = new JobScheduleOverride { CronExpression = "0 */5 * * * *" }
                },
                ExpectedConcurrencyStamp = firstDefinition.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        await fixture.Store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-2", "revision-2"), 2),
            TestContext.Current.CancellationToken);
        await fixture.Store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                fixture.Scope,
                "release-2",
                "worker-a",
                "revision-2",
                [CreateTriggeredDeclaration("job-a")]),
            TestContext.Current.CancellationToken);
        await fixture.Store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-2",
            TestContext.Current.CancellationToken);
        var triggered = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();

        AssertStickyPolicyState(triggered.Policy, policy);
        triggered.Policy.ConcurrencyStamp.Should().NotBe(policy.ConcurrencyStamp);
        triggered.EffectiveConfiguration.Schedule.Should().BeNull();

        await fixture.Store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-3", "revision-3"), 3),
            TestContext.Current.CancellationToken);
        await fixture.Store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                fixture.Scope,
                "release-3",
                "worker-a",
                "revision-3",
                [CreateRecurringDeclaration("job-a")]),
            TestContext.Current.CancellationToken);
        await fixture.Store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-3",
            TestContext.Current.CancellationToken);
        var recurringAgain = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!.Definitions.Single();

        AssertStickyPolicyState(recurringAgain.Policy, policy);
        recurringAgain.Policy.ConcurrencyStamp.Should().NotBe(triggered.Policy.ConcurrencyStamp);
        recurringAgain.EffectiveConfiguration.Schedule!.CronExpression.Should().Be("0 */5 * * * *");
        recurringAgain.IsPolicyReviewOutdated.Should().BeTrue();
    }

    private static RecurringScheduleSynchronization CreateSynchronization(
        ActiveJobDefinition definition,
        long changeEpoch) => new()
    {
        Template = definition.CreateExecutionTemplate(),
        Schedule = definition.EffectiveConfiguration.Schedule!,
        ChangeEpoch = changeEpoch
    };

    private static async Task ActivateDefinitionAsync(StoreFixture fixture, JobDeclaration declaration)
    {
        await fixture.Store.StageReleaseAsync(
            new JobCatalogReleaseStage(CreateManifest("release-1", "revision-1"), 1),
            TestContext.Current.CancellationToken);
        await fixture.Store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                fixture.Scope,
                "release-1",
                "worker-a",
                "revision-1",
                [declaration]),
            TestContext.Current.CancellationToken);
        await fixture.Store.TryActivateReleaseAsync(
            fixture.Scope,
            "release-1",
            TestContext.Current.CancellationToken);
    }

    private static void AssertStickyPolicyState(JobPolicy actual, JobPolicy expected)
    {
        actual.Overrides.Should().Be(expected.Overrides);
        actual.ReviewedAgainstJobRevisionId.Should().Be(expected.ReviewedAgainstJobRevisionId);
        actual.RecurringScheduleEffectiveFromUtc.Should().Be(expected.RecurringScheduleEffectiveFromUtc);
        actual.UpdatedAtUtc.Should().Be(expected.UpdatedAtUtc);
    }

    private static JobPolicyOverrides CreateRichPolicyOverrides(DateTimeOffset replacementEnd) => new()
    {
        DisabledOverride = true,
        DisplayNameOverride = "Operator name",
        DescriptionOverride = new JobDescriptionOverride { Value = "Operator description" },
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

    private static void AssertRichEffectiveConfiguration(
        EffectiveJobConfiguration effective,
        string configuredTimeZoneId,
        DateTimeOffset replacementEnd)
    {
        effective.JobName.Should().Be("Operator name");
        effective.Description.Should().Be("Operator description");
        effective.IsDisabled.Should().BeTrue();
        effective.MaxConcurrency.Should().Be(4);
        effective.RetryCount.Should().Be(3);
        effective.MaxExecutionTimeout.Should().Be(TimeSpan.FromMinutes(12));
        effective.MaxRetainedHistoryRecords.Should().Be(0);
        effective.MaxRetentionDays.Should().Be(30);
        effective.Schedule.Should().BeEquivalentTo(new RecurringScheduleDefinition
        {
            CronExpression = "0 */5 * * * *",
            TimeZoneId = configuredTimeZoneId,
            StartTimeUtc = null,
            EndTimeUtc = replacementEnd
        });
    }
}
