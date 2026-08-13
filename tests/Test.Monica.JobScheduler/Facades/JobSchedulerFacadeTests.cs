using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Models.Operations;
using Monica.JobScheduler.Providers;
using Monica.Modules;
using Monica.Testing.Results;
using Xunit;

namespace Test.Monica.JobScheduler.Facades;

public sealed class JobSchedulerFacadeTests
{
    private static readonly DateTimeOffset NOW = new(2026, 8, 13, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TriggerAsync_ShouldResolveAndCaptureTheActiveRevisionInsideTheStore()
    {
        var fixture = await CreateFixtureAsync();

        var result = await fixture.Facade.TriggerAsync(new JobTriggerRequest
        {
            JobKey = fixture.Definition.Declaration.JobKey,
            JobArgs = "{}",
            ExpectedOwnerId = fixture.Definition.OwnerId,
            ExpectedJobRevisionId = fixture.Definition.JobRevisionId
        }, TestContext.Current.CancellationToken);

        var execution = result.ShouldSucceed()!;
        execution.State.Should().Be(JobExecutionState.Queued);
        execution.AvailableAtUtc.Should().Be(NOW);
        execution.Template.Revision.JobRevisionId.Should().Be(fixture.Definition.JobRevisionId);
        execution.Template.Revision.WorkerRevisionId.Should().Be(fixture.Definition.WorkerRevisionId);
    }

    [Fact]
    public async Task TriggerAsync_WhenRetryOmitsAvailabilityAfterClockAdvances_ShouldReturnOriginalExecution()
    {
        var fixture = await CreateFixtureAsync();
        var request = new JobTriggerRequest
        {
            InstanceId = "operator-idempotency-key",
            JobKey = fixture.Definition.Declaration.JobKey,
            JobArgs = "{}"
        };

        var first = (await fixture.Facade.TriggerAsync(
            request,
            TestContext.Current.CancellationToken)).ShouldSucceed()!;
        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var retry = (await fixture.Facade.TriggerAsync(
            request,
            TestContext.Current.CancellationToken)).ShouldSucceed()!;

        first.AvailableAtUtc.Should().Be(NOW);
        retry.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task GetOverviewAsync_ShouldCombinePublicationCapabilityAndQueueSignals()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Facade.TriggerAsync(new JobTriggerRequest
        {
            JobKey = fixture.Definition.Declaration.JobKey,
            JobArgs = "{}"
        }, TestContext.Current.CancellationToken);

        var overview = (await fixture.Facade.GetOverviewAsync(TestContext.Current.CancellationToken)).ShouldSucceed()!;

        overview.IsConverged.Should().BeTrue();
        overview.Owners.Should().ContainSingle(owner =>
            owner.OwnerId == "owner-a" && owner.ActiveWorkerCount == 1 && owner.HasPublishedSnapshot);
        overview.ExecutionStateCounts[JobExecutionState.Queued].Should().Be(1);
        overview.RecentExecutions.Should().ContainSingle();
    }

    [Fact]
    public async Task QueryOperationalSummariesAsync_ShouldCombineDefinitionExecutionAndCapabilitySignals()
    {
        var fixture = await CreateFixtureAsync();
        var execution = (await fixture.Facade.TriggerAsync(new JobTriggerRequest
        {
            JobKey = fixture.Definition.Declaration.JobKey,
            JobArgs = "{}"
        }, TestContext.Current.CancellationToken)).ShouldSucceed()!;

        var page = (await fixture.Facade.QueryOperationalSummariesAsync(
            new JobCatalogQuery(),
            TestContext.Current.CancellationToken)).ShouldSucceed()!;

        var summary = page.Items.Should().ContainSingle().Subject;
        summary.Definition.Should().Be(fixture.Definition);
        summary.RecurringScheduleStatus.Should().Be(JobRecurringScheduleStatus.NotRecurring);
        summary.NextOccurrenceUtc.Should().BeNull();
        summary.LatestExecution!.InstanceId.Should().Be(execution.InstanceId);
        summary.LatestExecution.History.Should().BeEmpty();
        summary.QueuedExecutionCount.Should().Be(1);
        summary.RunningExecutionCount.Should().Be(0);
        summary.ActiveExecutionCount.Should().Be(1);
        summary.CompatibleWorkerCount.Should().Be(1);
        summary.HasCompatibleWorker.Should().BeTrue();

        var exact = (await fixture.Facade.GetOperationalSummaryAsync(
            fixture.Definition.Declaration.JobKey,
            TestContext.Current.CancellationToken)).ShouldSucceed();
        exact.Should().BeEquivalentTo(summary);
        (await fixture.Facade.GetOperationalSummaryAsync(
            "Jobs.Missing",
            TestContext.Current.CancellationToken)).ShouldSucceed().Should().BeNull();
    }

    [Fact]
    public async Task UpdatePolicyAsync_ShouldLeaveTheImmutableDeclarationUnchanged()
    {
        var fixture = await CreateFixtureAsync();
        var originalDeclaration = fixture.Definition.Declaration;

        var policy = (await fixture.Facade.UpdatePolicyAsync(
            fixture.Definition.OwnerId,
            fixture.Definition.Declaration.JobKey,
            new JobPolicyChange
            {
                DisabledOverride = true,
                MaxRetainedHistoryRecords = 25,
                MaxRetentionDays = 7,
                ExpectedConcurrencyStamp = fixture.Definition.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken)).ShouldSucceed()!;
        var definition = (await fixture.Facade.GetDefinitionAsync(
            fixture.Definition.Declaration.JobKey,
            TestContext.Current.CancellationToken)).ShouldSucceed();

        policy.DisabledOverride.Should().BeTrue();
        definition.Should().NotBeNull();
        definition!.Declaration.Should().Be(originalDeclaration);
        definition.IsDisabled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePoliciesAsync_WhenOneItemFails_ShouldReturnPartialResults()
    {
        var fixture = await CreateFixtureAsync();

        var batch = (await fixture.Facade.UpdatePoliciesAsync(new JobPolicyBatchUpdateRequest
        {
            Items =
            [
                new JobPolicyBatchUpdateItem
                {
                    OwnerId = fixture.Definition.OwnerId,
                    JobKey = fixture.Definition.Declaration.JobKey,
                    DisabledOverride = true,
                    MaxRetainedHistoryRecords = fixture.Definition.Policy.MaxRetainedHistoryRecords,
                    MaxRetentionDays = fixture.Definition.Policy.MaxRetentionDays,
                    ExpectedConcurrencyStamp = fixture.Definition.Policy.ConcurrencyStamp
                },
                new JobPolicyBatchUpdateItem
                {
                    OwnerId = "missing-owner",
                    JobKey = "Jobs.Missing",
                    DisabledOverride = true,
                    MaxRetainedHistoryRecords = 100,
                    MaxRetentionDays = null,
                    ExpectedConcurrencyStamp = "missing-stamp"
                }
            ]
        }, TestContext.Current.CancellationToken)).ShouldSucceed()!;

        batch.SucceededCount.Should().Be(1);
        batch.FailedCount.Should().Be(1);
        batch.Items[0].Policy!.DisabledOverride.Should().BeTrue();
        batch.Items[0].Error.Should().BeNull();
        batch.Items[1].Policy.Should().BeNull();
        batch.Items[1].Error.Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        const string scope = "facade-tests";
        var timeProvider = new ManualTimeProvider(NOW);
        var store = new InMemoryJobSchedulerStore(timeProvider);
        await store.StageReleaseAsync(new JobCatalogReleaseStage(
            new JobCatalogReleaseManifest(
                scope,
                "release-a",
                [new JobCatalogOwnerManifest("owner-a", "revision-a")]),
            1));
        await store.PublishOwnerSnapshotAsync(new JobOwnerCatalogSnapshot(
            scope,
            "release-a",
            "owner-a",
            "revision-a",
            [new JobDeclaration
            {
                JobKey = "Jobs.GenerateReport",
                JobArgsKey = "Jobs.GenerateReportArgs",
                JobName = "Generate report",
                JobType = JobType.Triggered,
                MaxConcurrency = 2
            }]));
        await store.TryActivateReleaseAsync(scope, "release-a");
        var definition = (await store.GetActiveCatalogAsync(scope))!.Definitions.Single();
        await store.RegisterWorkerCapabilityAsync(
            new WorkerCapabilityRegistration
            {
                SchedulerScopeKey = scope,
                OwnerKey = "owner-a",
                WorkerRevisionId = "revision-a",
                WorkerInstanceId = "worker-a-1",
                JobRevisionIds = [definition.JobRevisionId]
            },
            TimeSpan.FromMinutes(1));
        var options = new ModuleJobSchedulerOption { SchedulerScopeKey = scope };
        var facade = new JobSchedulerFacade(
            store,
            Options.Create(options),
            timeProvider,
            NullLogger<JobSchedulerFacade>.Instance);
        return new Fixture(facade, definition, timeProvider);
    }

    private sealed record Fixture(
        JobSchedulerFacade Facade,
        ActiveJobDefinition Definition,
        ManualTimeProvider TimeProvider);

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
