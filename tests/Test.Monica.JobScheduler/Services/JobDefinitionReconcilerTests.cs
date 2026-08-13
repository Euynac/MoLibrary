using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Providers;
using Monica.JobScheduler.Services;
using Monica.Modules;
using Monica.Testing.Doubles;
using Xunit;

namespace Test.Monica.JobScheduler.Services;

public sealed class JobDefinitionReconcilerTests
{
    private const string OWNER_PROJECT = "Worker.Project";
    private const string SCHEDULER_SCOPE = "definition-reconciliation-tests";
    private static readonly DateTime FIRST_BUILD = new(2026, 8, 12, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ApplyAsync_WhenSnapshotIsRepeated_ShouldPersistAndNotifyExactlyOnce()
    {
        var fixture = CreateFixture();
        var snapshot = CreateSnapshot(FIRST_BUILD, CreateDefinition("Worker.Project.JobA"));

        var first = await fixture.Reconciler.ApplyAsync(
            snapshot,
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);
        var second = await fixture.Reconciler.ApplyAsync(
            snapshot,
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);

        first.UpdatedDefinitions.Should().ContainSingle().Which.JobKey.Should().Be("Worker.Project.JobA");
        second.WasDuplicate.Should().BeTrue();
        fixture.Notifications.Should().ContainSingle();
        (await GetOwnedDefinitionsAsync(fixture.Repository)).Should().ContainSingle();
    }

    [Fact]
    public async Task ApplyAsync_WhenNewBuildChangesAndRemovesJobs_ShouldReplaceCodeStateAndPreserveOperatorState()
    {
        var fixture = CreateFixture();
        await fixture.Reconciler.ApplyAsync(
            CreateSnapshot(
                FIRST_BUILD,
                CreateDefinition("Worker.Project.JobA"),
                CreateDefinition("Worker.Project.JobB")),
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);

        var operated = (await fixture.Repository.GetDefinitionAsync(
            "Worker.Project.JobA",
            TestContext.Current.CancellationToken))!;
        operated.IsDisabled = true;
        operated.MaxRetainedHistoryRecords = 17;
        operated.MaxRetentionDays = 9;
        await fixture.Repository.SaveDefinitionAsync(operated, TestContext.Current.CancellationToken);

        var changed = CreateDefinition("Worker.Project.JobA");
        changed.JobName = "Updated declaration";
        changed.MaxConcurrency = 4;
        var result = await fixture.Reconciler.ApplyAsync(
            CreateSnapshot(FIRST_BUILD.AddMinutes(1), changed),
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);

        result.AddedDefinitions.Should().BeEmpty();
        result.UpdatedDefinitions.Should().ContainSingle().Which.JobKey.Should().Be(changed.JobKey);
        result.DeletedJobKeys.Should().Equal("Worker.Project.JobB");

        var refreshed = await fixture.Repository.GetDefinitionAsync(
            changed.JobKey,
            TestContext.Current.CancellationToken);
        refreshed.Should().NotBeNull();
        refreshed!.JobName.Should().Be("Updated declaration");
        refreshed.MaxConcurrency.Should().Be(4);
        refreshed.IsDisabled.Should().BeTrue();
        refreshed.MaxRetainedHistoryRecords.Should().Be(17);
        refreshed.MaxRetentionDays.Should().Be(9);

        var retired = await fixture.Repository.GetDefinitionAsync(
            "Worker.Project.JobB",
            TestContext.Current.CancellationToken);
        retired.Should().NotBeNull();
        retired!.IsDeleted.Should().BeTrue();
        retired.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ApplyAsync_WhenNotificationFailsAfterPersistence_ShouldRetryACompleteRefreshBeforeCommittingCursor()
    {
        var fixture = CreateFixture();
        var snapshot = CreateSnapshot(
            FIRST_BUILD,
            CreateDefinition("Worker.Project.JobA"),
            CreateDefinition("Worker.Project.JobB"));
        var notificationAttempt = 0;

        async Task FailFirstNotification(
            JobDefinitionSnapshotApplyResult result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            notificationAttempt++;
            if (notificationAttempt == 1)
            {
                throw new InvalidOperationException("Injected notification failure.");
            }

            await fixture.NotifyAsync(result, cancellationToken);
        }

        await fixture.Reconciler.Invoking(reconciler => reconciler.ApplyAsync(
                snapshot,
                FailFirstNotification,
                TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>();

        var retry = await fixture.Reconciler.ApplyAsync(
            snapshot,
            FailFirstNotification,
            TestContext.Current.CancellationToken);

        retry.AddedDefinitions.Should().BeEmpty();
        retry.UpdatedDefinitions.Select(static definition => definition.JobKey)
            .Should().BeEquivalentTo("Worker.Project.JobA", "Worker.Project.JobB");
        fixture.Notifications.Should().ContainSingle();
        fixture.Notifications[0].UpdatedDefinitions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ApplyAsync_WhenExistingCatalogHasNoCursor_ShouldRefreshCurrentJobsAndRetireMissingJobs()
    {
        var fixture = CreateFixture();
        await fixture.Repository.SaveDefinitionAsync(
            CreateDefinition("Worker.Project.JobA"),
            TestContext.Current.CancellationToken);
        await fixture.Repository.SaveDefinitionAsync(
            CreateDefinition("Worker.Project.RemovedJob"),
            TestContext.Current.CancellationToken);

        var result = await fixture.Reconciler.ApplyAsync(
            CreateSnapshot(FIRST_BUILD, CreateDefinition("Worker.Project.JobA")),
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);

        result.AddedDefinitions.Should().BeEmpty();
        result.UpdatedDefinitions.Should().ContainSingle().Which.JobKey.Should().Be("Worker.Project.JobA");
        result.DeletedJobKeys.Should().Equal("Worker.Project.RemovedJob");
        (await fixture.Repository.GetDefinitionAsync(
                "Worker.Project.RemovedJob",
                TestContext.Current.CancellationToken))!
            .IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyAsync_WhenJobKeyBelongsToAnotherProject_ShouldRejectOwnershipCollision()
    {
        var fixture = CreateFixture();
        var existing = CreateDefinition("Shared.Namespace.Job");
        existing.FromProject = "Another.Project";
        await fixture.Repository.SaveDefinitionAsync(existing, TestContext.Current.CancellationToken);

        await fixture.Reconciler.Invoking(reconciler => reconciler.ApplyAsync(
                CreateSnapshot(FIRST_BUILD, CreateDefinition(existing.JobKey)),
                fixture.NotifyAsync,
                TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already owned by project 'Another.Project'*'Worker.Project'*");

        (await fixture.Repository.GetDefinitionAsync(
                existing.JobKey,
                TestContext.Current.CancellationToken))!
            .FromProject.Should().Be("Another.Project");
        fixture.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_WhenOlderReplicaPublishesAfterNewerBuild_ShouldIgnoreIt()
    {
        var fixture = CreateFixture();
        var newerDefinition = CreateDefinition("Worker.Project.JobA");
        newerDefinition.JobName = "New deployment";
        await fixture.Reconciler.ApplyAsync(
            CreateSnapshot(FIRST_BUILD.AddMinutes(1), newerDefinition),
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);

        var staleDefinition = CreateDefinition("Worker.Project.JobA");
        staleDefinition.JobName = "Old deployment";
        var result = await fixture.Reconciler.ApplyAsync(
            CreateSnapshot(FIRST_BUILD, staleDefinition),
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);

        result.WasStale.Should().BeTrue();
        fixture.Notifications.Should().ContainSingle();
        (await fixture.Repository.GetDefinitionAsync(
                staleDefinition.JobKey,
                TestContext.Current.CancellationToken))!
            .JobName.Should().Be("New deployment");
    }

    [Fact]
    public async Task ApplyAsync_WhenNewerBuildHasSameDefinitions_ShouldAdvanceCursorWithoutNotifyingAgain()
    {
        var fixture = CreateFixture();
        var definition = CreateDefinition("Worker.Project.JobA");
        await fixture.Reconciler.ApplyAsync(
            CreateSnapshot(FIRST_BUILD, definition),
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);

        var result = await fixture.Reconciler.ApplyAsync(
            CreateSnapshot(FIRST_BUILD.AddMinutes(1), definition),
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);

        result.RequiresNotification.Should().BeFalse();
        result.WasDuplicate.Should().BeFalse();
        result.WasStale.Should().BeFalse();
        fixture.Notifications.Should().ContainSingle();

        var stale = await fixture.Reconciler.ApplyAsync(
            CreateSnapshot(FIRST_BUILD, definition),
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);
        stale.WasStale.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyAsync_WhenEmptySnapshotIsNewer_ShouldRetireEveryDefinitionOwnedByProject()
    {
        var fixture = CreateFixture();
        await fixture.Reconciler.ApplyAsync(
            CreateSnapshot(FIRST_BUILD, CreateDefinition("Worker.Project.JobA")),
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);

        var result = await fixture.Reconciler.ApplyAsync(
            CreateSnapshot(FIRST_BUILD.AddMinutes(1)),
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);

        result.DeletedJobKeys.Should().Equal("Worker.Project.JobA");
        var definition = await fixture.Repository.GetDefinitionAsync(
            "Worker.Project.JobA",
            TestContext.Current.CancellationToken);
        definition.Should().NotBeNull();
        definition!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyAsync_WhenLaterSnapshotChangesOtherDefinitions_ShouldNotReportHistoricalDeletionAgain()
    {
        var fixture = CreateFixture();
        await fixture.Reconciler.ApplyAsync(
            CreateSnapshot(
                FIRST_BUILD,
                CreateDefinition("Worker.Project.JobA"),
                CreateDefinition("Worker.Project.JobB")),
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);
        await fixture.Reconciler.ApplyAsync(
            CreateSnapshot(FIRST_BUILD.AddMinutes(1), CreateDefinition("Worker.Project.JobA")),
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);

        var changed = CreateDefinition("Worker.Project.JobA");
        changed.JobName = "Later declaration";
        var result = await fixture.Reconciler.ApplyAsync(
            CreateSnapshot(FIRST_BUILD.AddMinutes(2), changed),
            fixture.NotifyAsync,
            TestContext.Current.CancellationToken);

        result.UpdatedDefinitions.Should().ContainSingle().Which.JobName.Should().Be("Later declaration");
        result.DeletedJobKeys.Should().BeEmpty();
    }

    private static ReconcilerFixture CreateFixture()
    {
        var options = Options.Create(new ModuleJobSchedulerOption());
        options.Value.SchedulerScopeKey = SCHEDULER_SCOPE;
        var repository = new InMemoryJobMetadataRepository(
            NullLogger<InMemoryJobMetadataRepository>.Instance,
            options);
        var reconciler = new JobDefinitionReconciler(
            repository,
            new InMemoryDistributedStateStore(),
            options,
            NullLogger<JobDefinitionReconciler>.Instance);
        return new ReconcilerFixture(repository, reconciler);
    }

    private static JobDefinitionSnapshotPublishedEvent CreateSnapshot(
        DateTime buildTime,
        params JobDefinition[] definitions)
    {
        return JobDefinitionSnapshotPublishedEvent.Create(
            SCHEDULER_SCOPE,
            OWNER_PROJECT,
            sourceAppId: "worker-app",
            sourceInstanceId: "worker-instance",
            buildTime,
            sourceReleaseVersion: "test-release",
            definitions);
    }

    private static JobDefinition CreateDefinition(string jobKey)
    {
        return new JobDefinition
        {
            SchedulerScopeKey = SCHEDULER_SCOPE,
            JobKey = jobKey,
            FromProject = OWNER_PROJECT,
            JobName = jobKey,
            JobType = JobType.Recurring,
            MaxConcurrency = 1,
            RetryCount = 0,
            MaxExecutionTimeout = TimeSpan.FromMinutes(5),
            CronExpression = "0 0 0 * * *"
        };
    }

    private static async Task<IReadOnlyList<JobDefinition>> GetOwnedDefinitionsAsync(
        IJobMetadataRepository repository)
    {
        return (await repository.QueryDefinitionsAsync(
            new JobDefinitionQuery
            {
                FromProject = OWNER_PROJECT,
                IncludeDeleted = true,
                PageSize = int.MaxValue
            },
            TestContext.Current.CancellationToken)).Items;
    }

    private sealed class ReconcilerFixture(
        InMemoryJobMetadataRepository repository,
        JobDefinitionReconciler reconciler)
    {
        internal InMemoryJobMetadataRepository Repository { get; } = repository;
        internal JobDefinitionReconciler Reconciler { get; } = reconciler;
        internal List<JobDefinitionSnapshotApplyResult> Notifications { get; } = [];

        internal Task NotifyAsync(
            JobDefinitionSnapshotApplyResult result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Notifications.Add(result);
            return Task.CompletedTask;
        }
    }
}
