using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.DependencyInjection.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.EfCore;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Analytics;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Providers;
using Monica.Modules;
using Monica.Repository.Persistence.Services;
using Xunit;

namespace Test.Monica.JobScheduler.Stores.Execution;

public sealed class JobSchedulerAnalyticsStoreTests
{
    private static readonly DateTimeOffset RANGE_START = new(2026, 8, 2, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Analytics_ShouldSeparateCreatedCohortFromCompletionWindowAndCalculateDurationDistribution(
        bool useEfCore)
    {
        await using var fixture = await AnalyticsStoreFixture.CreateAsync(
            useEfCore,
            RANGE_START.AddMinutes(-10),
            TestContext.Current.CancellationToken);
        var definitions = await fixture.ActivateAsync(TestContext.Current.CancellationToken);
        var capability = await fixture.Store.RegisterWorkerCapabilityAsync(new WorkerCapabilityRegistration
        {
            SchedulerScopeKey = fixture.Scope,
            OwnerKey = "analytics-owner",
            WorkerRevisionId = "analytics-v1",
            WorkerInstanceId = "analytics-worker",
            JobRevisionIds = definitions.Values.Select(static item => item.JobRevisionId).ToArray()
        }, TimeSpan.FromHours(12), TestContext.Current.CancellationToken);

        await fixture.EnqueueAsync(definitions["jobs.alpha"], "before-range");
        var beforeRange = await fixture.ClaimOneAsync(capability);
        fixture.Time.Advance(TimeSpan.FromMinutes(20));
        await fixture.CompleteAsync(beforeRange, JobAttemptOutcome.Succeeded);

        await fixture.EnqueueAsync(definitions["jobs.alpha"], "failed-alpha");
        var failed = await fixture.ClaimOneAsync(capability);
        fixture.Time.Advance(TimeSpan.FromMinutes(40));
        await fixture.CompleteAsync(failed, JobAttemptOutcome.Failed);

        await fixture.EnqueueAsync(definitions["jobs.beta"], "succeeded-beta");
        var succeeded = await fixture.ClaimOneAsync(capability);
        fixture.Time.Advance(TimeSpan.FromMinutes(70));
        await fixture.CompleteAsync(succeeded, JobAttemptOutcome.Succeeded);

        await fixture.EnqueueAsync(definitions["jobs.alpha"], "still-queued");
        await fixture.EnqueueAsync(definitions["jobs.beta"], "cancelled-beta");
        await fixture.Store.RequestCancellationAsync(
            fixture.Scope,
            "cancelled-beta",
            cancellationToken: TestContext.Current.CancellationToken);

        var snapshot = await fixture.Store.GetExecutionAnalyticsAsync(
            fixture.Scope,
            new JobExecutionAnalyticsQuery
            {
                StartTimeUtc = RANGE_START,
                EndTimeUtc = RANGE_START.AddHours(4),
                BucketSize = JobExecutionAnalyticsBucketSize.Hour,
                TopJobLimit = 5,
                SlowestExecutionLimit = 5
            },
            TestContext.Current.CancellationToken);

        snapshot.StateTotals[JobExecutionState.Succeeded].Should().Be(1);
        snapshot.StateTotals[JobExecutionState.Failed].Should().Be(1);
        snapshot.StateTotals[JobExecutionState.Queued].Should().Be(1);
        snapshot.StateTotals[JobExecutionState.Cancelled].Should().Be(1);
        snapshot.CompletedTerminalCount.Should().Be(4);
        snapshot.ExecutedTerminalCount.Should().Be(3);
        snapshot.ExecutedThroughputPerHour.Should().Be(0.75);
        snapshot.Reliability.Should().BeApproximately(2D / 3D, 0.000001);
        snapshot.SkipRate.Should().Be(0);
        snapshot.Duration.Count.Should().Be(3);
        snapshot.Duration.Minimum.Should().Be(TimeSpan.FromMinutes(20));
        snapshot.Duration.Average.Should().BeCloseTo(TimeSpan.FromMinutes(130D / 3D), TimeSpan.FromTicks(1));
        snapshot.Duration.P50.Should().Be(TimeSpan.FromMinutes(40));
        snapshot.Duration.P90.Should().Be(TimeSpan.FromMinutes(70));
        snapshot.Duration.P95.Should().Be(TimeSpan.FromMinutes(70));
        snapshot.Duration.P99.Should().Be(TimeSpan.FromMinutes(70));
        snapshot.Duration.Maximum.Should().Be(TimeSpan.FromMinutes(70));
        snapshot.Trend.Should().HaveCount(4);
        snapshot.Trend[0].SucceededCount.Should().Be(1);
        snapshot.Trend[0].FailedCount.Should().Be(1);
        snapshot.Trend[1].CompletedTerminalCount.Should().Be(0);
        snapshot.Trend[2].SucceededCount.Should().Be(1);
        snapshot.Trend[2].CancelledCount.Should().Be(1);
        snapshot.TopJobsByVolume.Select(static item => item.JobKey).Should().Equal("jobs.alpha", "jobs.beta");
        snapshot.TopJobsByFailures.Should().ContainSingle().Which.JobKey.Should().Be("jobs.alpha");
        snapshot.TopJobsBySkips.Should().BeEmpty();
        snapshot.SlowestExecutions.Should().HaveCount(3);
        snapshot.SlowestExecutions[0].InstanceId.Should().Be("succeeded-beta");
        snapshot.SlowestExecutions[0].Duration.Should().Be(TimeSpan.FromMinutes(70));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Analytics_WhenJobKeyIsSelected_ShouldFilterEveryProjection(bool useEfCore)
    {
        await using var fixture = await AnalyticsStoreFixture.CreateAsync(
            useEfCore,
            RANGE_START,
            TestContext.Current.CancellationToken);
        var definitions = await fixture.ActivateAsync(TestContext.Current.CancellationToken);
        await fixture.EnqueueAsync(definitions["jobs.alpha"], "alpha-queued");
        await fixture.EnqueueAsync(definitions["jobs.beta"], "beta-queued");

        var snapshot = await fixture.Store.GetExecutionAnalyticsAsync(
            fixture.Scope,
            new JobExecutionAnalyticsQuery
            {
                StartTimeUtc = RANGE_START,
                EndTimeUtc = RANGE_START.AddHours(1),
                JobKey = "jobs.beta"
            },
            TestContext.Current.CancellationToken);

        snapshot.JobKey.Should().Be("jobs.beta");
        snapshot.StateTotals.Values.Sum().Should().Be(1);
        snapshot.StateTotals[JobExecutionState.Queued].Should().Be(1);
        snapshot.Trend.Should().ContainSingle();
        snapshot.TopJobsByVolume.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Analytics_WhenRecurringCapacitySkipsAnOccurrence_ShouldReportSkipSeparately(bool useEfCore)
    {
        await using var fixture = await AnalyticsStoreFixture.CreateAsync(
            useEfCore,
            RANGE_START,
            TestContext.Current.CancellationToken);
        var recurring = await fixture.ActivateRecurringAsync(TestContext.Current.CancellationToken);
        var catalog = (await fixture.Store.GetActiveCatalogAsync(
            fixture.Scope,
            TestContext.Current.CancellationToken))!;
        var synchronized = await fixture.Store.SynchronizeRecurringScheduleAsync(
            new RecurringScheduleSynchronization
            {
                Template = recurring.CreateExecutionTemplate(),
                Schedule = new RecurringScheduleDefinition
                {
                    CronExpression = recurring.Declaration.CronExpression!,
                    TimeZoneId = recurring.Declaration.TimeZoneId!
                },
                ChangeEpoch = catalog.Version.ChangeEpoch
            },
            TestContext.Current.CancellationToken);
        var firstOccurrence = synchronized.Cursor!.NextOccurrenceUtc!.Value;
        fixture.Time.Advance(firstOccurrence - fixture.Time.GetUtcNow());
        var first = await fixture.Store.TryMaterializeRecurringOccurrenceAsync(
            new RecurringOccurrenceMaterialization
            {
                CursorKey = synchronized.Cursor.Key,
                ExpectedVersion = synchronized.Cursor.Version,
                ExpectedOccurrenceUtc = firstOccurrence,
                NextOccurrenceUtc = firstOccurrence.AddMinutes(1),
                InstanceId = "recurring-first"
            },
            TestContext.Current.CancellationToken);
        fixture.Time.Advance(TimeSpan.FromMinutes(1));
        var skipped = await fixture.Store.TryMaterializeRecurringOccurrenceAsync(
            new RecurringOccurrenceMaterialization
            {
                CursorKey = first.Cursor!.Key,
                ExpectedVersion = first.Cursor.Version,
                ExpectedOccurrenceUtc = first.Cursor.NextOccurrenceUtc!.Value,
                NextOccurrenceUtc = first.Cursor.NextOccurrenceUtc.Value.AddMinutes(1),
                InstanceId = "recurring-skipped"
            },
            TestContext.Current.CancellationToken);

        var snapshot = await fixture.Store.GetExecutionAnalyticsAsync(
            fixture.Scope,
            new JobExecutionAnalyticsQuery
            {
                StartTimeUtc = RANGE_START,
                EndTimeUtc = fixture.Time.GetUtcNow().AddSeconds(1),
                BucketSize = JobExecutionAnalyticsBucketSize.Hour
            },
            TestContext.Current.CancellationToken);

        first.Execution!.State.Should().Be(JobExecutionState.Queued);
        skipped.Execution!.State.Should().Be(JobExecutionState.Skipped);
        snapshot.StateTotals[JobExecutionState.Queued].Should().Be(1);
        snapshot.StateTotals[JobExecutionState.Skipped].Should().Be(1);
        snapshot.CompletedTerminalCount.Should().Be(1);
        snapshot.ExecutedTerminalCount.Should().Be(0);
        snapshot.Reliability.Should().Be(0);
        snapshot.SkipRate.Should().Be(1);
        snapshot.Duration.Count.Should().Be(0);
        snapshot.TopJobsBySkips.Should().ContainSingle().Which.SkippedCount.Should().Be(1);
        snapshot.SlowestExecutions.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyticsQuery_WhenBoundsAreInvalid_ShouldRejectBeforeReadingTheStore()
    {
        var store = new InMemoryJobSchedulerStore(new ManualTimeProvider(RANGE_START));

        var excessiveRange = async () => await store.GetExecutionAnalyticsAsync(
            "analytics-tests",
            new JobExecutionAnalyticsQuery
            {
                StartTimeUtc = RANGE_START,
                EndTimeUtc = RANGE_START.AddDays(32),
                BucketSize = JobExecutionAnalyticsBucketSize.Hour
            },
            TestContext.Current.CancellationToken);
        var excessiveRanking = async () => await store.GetExecutionAnalyticsAsync(
            "analytics-tests",
            new JobExecutionAnalyticsQuery
            {
                StartTimeUtc = RANGE_START,
                EndTimeUtc = RANGE_START.AddDays(1),
                TopJobLimit = JobExecutionAnalyticsQuery.MAX_TOP_JOB_COUNT + 1
            },
            TestContext.Current.CancellationToken);

        await excessiveRange.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await excessiveRanking.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private sealed class AnalyticsStoreFixture : IAsyncDisposable
    {
        private readonly ServiceProvider? _serviceProvider;
        private readonly SqliteConnection? _connection;

        private AnalyticsStoreFixture(
            IJobSchedulerStore store,
            ManualTimeProvider time,
            ServiceProvider? serviceProvider = null,
            SqliteConnection? connection = null)
        {
            Store = store;
            Time = time;
            _serviceProvider = serviceProvider;
            _connection = connection;
        }

        internal string Scope => "analytics-tests";
        internal IJobSchedulerStore Store { get; }
        internal ManualTimeProvider Time { get; }

        internal static async Task<AnalyticsStoreFixture> CreateAsync(
            bool useEfCore,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var time = new ManualTimeProvider(now);
            if (!useEfCore)
            {
                return new AnalyticsStoreFixture(new InMemoryJobSchedulerStore(time), time);
            }

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions();
            services.AddSingleton<IOptions<ModuleRepositoryOption>>(Options.Create(new ModuleRepositoryOption()));
            services.AddSingleton<ICachedServiceProvider, global::Monica.DependencyInjection.Services.CachedServiceProvider>();
            var connection = new SqliteConnection(
                $"Data Source=analytics-store-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await connection.OpenAsync(cancellationToken);
            var provider = services.BuildServiceProvider();
            var factory = new AnalyticsDbContextFactory(connection.ConnectionString, provider);
            await using (var dbContext = await factory.CreateDbContextAsync(cancellationToken))
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            }

            return new AnalyticsStoreFixture(
                new EfCoreJobSchedulerStore(factory, time),
                time,
                provider,
                connection);
        }

        internal async Task<IReadOnlyDictionary<string, ActiveJobDefinition>> ActivateAsync(
            CancellationToken cancellationToken)
        {
            await Store.StageReleaseAsync(
                new JobCatalogReleaseStage(
                    new JobCatalogReleaseManifest(
                        Scope,
                        "analytics-release",
                        [new JobCatalogOwnerManifest("analytics-owner", "analytics-v1")]),
                    1),
                cancellationToken);
            await Store.PublishOwnerSnapshotAsync(new JobOwnerCatalogSnapshot(
                Scope,
                "analytics-release",
                "analytics-owner",
                "analytics-v1",
                [CreateDeclaration("jobs.alpha"), CreateDeclaration("jobs.beta")]),
                cancellationToken);
            await Store.TryActivateReleaseAsync(Scope, "analytics-release", cancellationToken);
            return (await Store.GetActiveCatalogAsync(Scope, cancellationToken))!.Definitions
                .ToDictionary(static item => item.Declaration.JobKey, StringComparer.Ordinal);
        }

        internal async Task<ActiveJobDefinition> ActivateRecurringAsync(CancellationToken cancellationToken)
        {
            await Store.StageReleaseAsync(
                new JobCatalogReleaseStage(
                    new JobCatalogReleaseManifest(
                        Scope,
                        "recurring-release",
                        [new JobCatalogOwnerManifest("analytics-owner", "analytics-v1")]),
                    1),
                cancellationToken);
            await Store.PublishOwnerSnapshotAsync(new JobOwnerCatalogSnapshot(
                Scope,
                "recurring-release",
                "analytics-owner",
                "analytics-v1",
                [new JobDeclaration
                {
                    JobKey = "jobs.recurring",
                    JobName = "Recurring analytics job",
                    JobType = JobType.Recurring,
                    CronExpression = "0 * * * * *",
                    TimeZoneId = TimeZoneInfo.Utc.Id,
                    MaxConcurrency = 1,
                    MaxExecutionTimeout = TimeSpan.FromMinutes(5)
                }]),
                cancellationToken);
            await Store.TryActivateReleaseAsync(Scope, "recurring-release", cancellationToken);
            return (await Store.GetActiveCatalogAsync(Scope, cancellationToken))!.Definitions.Single();
        }

        internal Task<JobExecutionInstance> EnqueueAsync(ActiveJobDefinition definition, string instanceId) =>
            Store.EnqueueAsync(new JobEnqueueRequest
            {
                InstanceId = instanceId,
                SchedulerScopeKey = Scope,
                JobKey = definition.Declaration.JobKey,
                ExpectedOwnerId = definition.OwnerId,
                ExpectedJobRevisionId = definition.JobRevisionId,
                JobArgs = "{}",
                AvailableAtUtc = Time.GetUtcNow()
            }, TestContext.Current.CancellationToken);

        internal async Task<JobExecutionLease> ClaimOneAsync(WorkerCapabilityLease capability) =>
            (await Store.ClaimAsync(new JobClaimRequest
            {
                CapabilityLeaseKey = capability.LeaseKey,
                LeaseDuration = TimeSpan.FromHours(2),
                MaxCount = 1
            }, TestContext.Current.CancellationToken)).Single();

        internal Task<JobAttemptCompletionResult> CompleteAsync(
            JobExecutionLease lease,
            JobAttemptOutcome outcome) => Store.CompleteAttemptAsync(new JobAttemptCompletion
        {
            LeaseKey = lease.LeaseKey,
            Outcome = outcome
        }, TestContext.Current.CancellationToken);

        public async ValueTask DisposeAsync()
        {
            if (_serviceProvider is not null)
            {
                await _serviceProvider.DisposeAsync();
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }
        }

        private static JobDeclaration CreateDeclaration(string jobKey) => new()
        {
            JobKey = jobKey,
            JobArgsKey = $"{jobKey}.Args",
            JobName = jobKey,
            JobType = JobType.Triggered,
            MaxConcurrency = 1,
            RetryCount = 0,
            MaxExecutionTimeout = TimeSpan.FromHours(2)
        };
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        internal void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private sealed class AnalyticsDbContextFactory(
        string connectionString,
        IServiceProvider serviceProvider) : IDbContextFactory<JobSchedulerDbContext>
    {
        public JobSchedulerDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<JobSchedulerDbContext>()
                .UseSqlite(connectionString)
                .Options,
            serviceProvider.GetRequiredService<ICachedServiceProvider>());

        public ValueTask<JobSchedulerDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(CreateDbContext());
        }
    }
}
