using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core.Results;
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
    public async Task RunRecurringNowAsync_ShouldAdmitDedicatedRecurringExecution()
    {
        var fixture = await CreateFixtureAsync(JobType.Recurring);
        _ = (await fixture.Facade.UpdatePolicyAsync(
            fixture.Definition.OwnerId,
            fixture.Definition.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = fixture.Definition.Policy.Overrides with { DisabledOverride = true },
                ExpectedConcurrencyStamp = fixture.Definition.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken)).ShouldSucceed();
        var request = new JobRecurringRunNowRequest
        {
            InstanceId = "operator-recurring-idempotency-key",
            JobKey = fixture.Definition.Declaration.JobKey,
            ExpectedOwnerId = fixture.Definition.OwnerId,
            ExpectedJobRevisionId = fixture.Definition.JobRevisionId
        };

        var first = (await fixture.Facade.RunRecurringNowAsync(
            request,
            TestContext.Current.CancellationToken)).ShouldSucceed()!;
        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var repeated = (await fixture.Facade.RunRecurringNowAsync(
            request,
            TestContext.Current.CancellationToken)).ShouldSucceed()!;

        first.State.Should().Be(JobExecutionState.Queued);
        first.Origin.Should().Be(JobExecutionOrigin.RecurringRunNow);
        first.RecurringOccurrenceUtc.Should().BeNull();
        first.AvailableAtUtc.Should().Be(NOW);
        repeated.Should().BeEquivalentTo(first);
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
                Overrides = new JobPolicyOverrides
                {
                    DisabledOverride = true,
                    DisplayNameOverride = "Operator report",
                    DescriptionOverride = new JobDescriptionOverride { Value = null },
                    MaxConcurrencyOverride = 4,
                    RetryCountOverride = 3,
                    MaxExecutionTimeoutOverride = TimeSpan.FromMinutes(20),
                    MaxRetainedHistoryRecords = 25,
                    MaxRetentionDays = 7
                },
                ExpectedConcurrencyStamp = fixture.Definition.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken)).ShouldSucceed()!;
        var definition = (await fixture.Facade.GetDefinitionAsync(
            fixture.Definition.Declaration.JobKey,
            TestContext.Current.CancellationToken)).ShouldSucceed();

        policy.Overrides.DisabledOverride.Should().BeTrue();
        definition.Should().NotBeNull();
        definition!.Declaration.Should().Be(originalDeclaration);
        definition.IsDisabled.Should().BeTrue();
        definition.EffectiveConfiguration.JobName.Should().Be("Operator report");
        definition.EffectiveConfiguration.Description.Should().BeNull();
        definition.EffectiveConfiguration.MaxConcurrency.Should().Be(4);
        definition.EffectiveConfiguration.RetryCount.Should().Be(3);
        definition.EffectiveConfiguration.MaxExecutionTimeout.Should().Be(TimeSpan.FromMinutes(20));
        policy.ReviewedAgainstJobRevisionId.Should().Be(definition.JobRevisionId);
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
                    Overrides = fixture.Definition.Policy.Overrides with { DisabledOverride = true },
                    ExpectedConcurrencyStamp = fixture.Definition.Policy.ConcurrencyStamp
                },
                new JobPolicyBatchUpdateItem
                {
                    OwnerId = "missing-owner",
                    JobKey = "Jobs.Missing",
                    Overrides = new JobPolicyOverrides { DisabledOverride = true },
                    ExpectedConcurrencyStamp = "missing-stamp"
                }
            ]
        }, TestContext.Current.CancellationToken)).ShouldSucceed()!;

        batch.SucceededCount.Should().Be(1);
        batch.FailedCount.Should().Be(1);
        batch.Items[0].Policy!.Overrides.DisabledOverride.Should().BeTrue();
        batch.Items[0].Error.Should().BeNull();
        batch.Items[0].FailureStatus.Should().BeNull();
        batch.Items[1].Policy.Should().BeNull();
        batch.Items[1].Error.Should().NotBeNullOrWhiteSpace();
        batch.Items[1].FailureStatus.Should().Be(ResStatus.NotFound);
    }

    [Fact]
    public async Task UpdatePolicyAsync_WhenPolicyRevisionIsStale_ShouldReturnConflictAndPreserveFirstWrite()
    {
        var fixture = await CreateFixtureAsync();
        var first = await fixture.Facade.UpdatePolicyAsync(
            fixture.Definition.OwnerId,
            fixture.Definition.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = fixture.Definition.Policy.Overrides with { DisplayNameOverride = "First write" },
                ExpectedConcurrencyStamp = fixture.Definition.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var conflict = await fixture.Facade.UpdatePolicyAsync(
            fixture.Definition.OwnerId,
            fixture.Definition.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = fixture.Definition.Policy.Overrides with { DisplayNameOverride = "Stale write" },
                ExpectedConcurrencyStamp = fixture.Definition.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var current = (await fixture.Facade.GetDefinitionAsync(
            fixture.Definition.Declaration.JobKey,
            TestContext.Current.CancellationToken)).ShouldSucceed();

        first.Status.Should().Be(ResStatus.Ok);
        conflict.Status.Should().Be(ResStatus.Conflict);
        conflict.Data.Should().BeNull();
        current!.EffectiveConfiguration.JobName.Should().Be("First write");
    }

    private static async Task<Fixture> CreateFixtureAsync(JobType jobType = JobType.Triggered)
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
                JobArgsKey = jobType == JobType.Triggered ? "Jobs.GenerateReportArgs" : null,
                JobName = "Generate report",
                JobType = jobType,
                MaxConcurrency = 2,
                CronExpression = jobType == JobType.Recurring ? "0 * * * * *" : null,
                TimeZoneId = jobType == JobType.Recurring ? TimeZoneInfo.Utc.Id : null
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
