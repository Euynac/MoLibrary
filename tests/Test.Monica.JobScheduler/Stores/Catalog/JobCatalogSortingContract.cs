using AwesomeAssertions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;

namespace Test.Monica.JobScheduler.Stores.Catalog;

internal static class JobCatalogSortingContract
{
    private const string RELEASE_ID = "catalog-sorting-release";
    private const string OWNER_ALPHA = "owner-alpha";
    private const string OWNER_BETA = "owner-beta";
    private const string WORKER_ALPHA = "worker-revision-alpha";
    private const string WORKER_BETA = "worker-revision-beta";
    private const string JOB_ALPHA = "jobs.alpha";
    private const string JOB_BRAVO = "jobs.bravo";
    private const string JOB_CHARLIE = "jobs.charlie";
    private const string JOB_DELTA = "jobs.delta";

    private static readonly CatalogOrderingExpectation[] EXPECTATIONS =
    [
        new(
            JobCatalogSortField.JobName,
            [JOB_CHARLIE, JOB_ALPHA, JOB_BRAVO, JOB_DELTA],
            [JOB_DELTA, JOB_ALPHA, JOB_BRAVO, JOB_CHARLIE]),
        new(
            JobCatalogSortField.JobKey,
            [JOB_ALPHA, JOB_BRAVO, JOB_CHARLIE, JOB_DELTA],
            [JOB_DELTA, JOB_CHARLIE, JOB_BRAVO, JOB_ALPHA]),
        new(
            JobCatalogSortField.OwnerId,
            [JOB_ALPHA, JOB_DELTA, JOB_BRAVO, JOB_CHARLIE],
            [JOB_BRAVO, JOB_CHARLIE, JOB_ALPHA, JOB_DELTA]),
        new(
            JobCatalogSortField.JobType,
            [JOB_BRAVO, JOB_DELTA, JOB_ALPHA, JOB_CHARLIE],
            [JOB_ALPHA, JOB_CHARLIE, JOB_BRAVO, JOB_DELTA]),
        new(
            JobCatalogSortField.IsDisabled,
            [JOB_ALPHA, JOB_BRAVO, JOB_CHARLIE, JOB_DELTA],
            [JOB_CHARLIE, JOB_DELTA, JOB_ALPHA, JOB_BRAVO])
    ];

    internal static async Task SeedAsync(
        IJobSchedulerStore store,
        string schedulerScopeKey,
        CancellationToken cancellationToken)
    {
        JobCatalogOwnerManifest[] owners =
        [
            new(OWNER_ALPHA, WORKER_ALPHA),
            new(OWNER_BETA, WORKER_BETA)
        ];
        await store.StageReleaseAsync(
            new JobCatalogReleaseStage(
                new JobCatalogReleaseManifest(schedulerScopeKey, RELEASE_ID, owners),
                1),
            cancellationToken);
        await store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                schedulerScopeKey,
                RELEASE_ID,
                OWNER_ALPHA,
                WORKER_ALPHA,
                [
                    CreateDeclaration(JOB_ALPHA, "Shared", JobType.Triggered, isDisabledByDefault: false),
                    CreateDeclaration(JOB_DELTA, "Zulu", JobType.Recurring, isDisabledByDefault: true)
                ]),
            cancellationToken);
        await store.PublishOwnerSnapshotAsync(
            new JobOwnerCatalogSnapshot(
                schedulerScopeKey,
                RELEASE_ID,
                OWNER_BETA,
                WORKER_BETA,
                [
                    CreateDeclaration(JOB_BRAVO, "shared", JobType.Recurring, isDisabledByDefault: false),
                    CreateDeclaration(JOB_CHARLIE, "Alpha", JobType.Triggered, isDisabledByDefault: true)
                ]),
            cancellationToken);

        var activation = await store.TryActivateReleaseAsync(
            schedulerScopeKey,
            RELEASE_ID,
            cancellationToken);

        activation.Status.Should().Be(JobCatalogActivationStatus.Activated);
    }

    internal static async Task AssertSupportedOrderingAsync(
        IJobSchedulerStore store,
        string schedulerScopeKey,
        CancellationToken cancellationToken)
    {
        foreach (var expectation in EXPECTATIONS)
        {
            await AssertDirectionAsync(
                store,
                schedulerScopeKey,
                expectation.SortField,
                descending: false,
                expectation.AscendingJobKeys,
                cancellationToken);
            await AssertDirectionAsync(
                store,
                schedulerScopeKey,
                expectation.SortField,
                descending: true,
                expectation.DescendingJobKeys,
                cancellationToken);
        }
    }

    internal static async Task AssertInvalidSortRejectedAsync(
        IJobSchedulerStore store,
        string schedulerScopeKey,
        CancellationToken cancellationToken)
    {
        var query = new JobCatalogQuery
        {
            SortField = (JobCatalogSortField)int.MaxValue,
            PageSize = 20
        };
        Func<Task> queryDefinitions = () => store.QueryActiveDefinitionsAsync(
            schedulerScopeKey,
            query,
            cancellationToken);
        Func<Task> querySummaries = () => store.QueryOperationalSummariesAsync(
            schedulerScopeKey,
            query,
            cancellationToken);

        await queryDefinitions.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(query));
        await querySummaries.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(query));
    }

    private static async Task AssertDirectionAsync(
        IJobSchedulerStore store,
        string schedulerScopeKey,
        JobCatalogSortField sortField,
        bool descending,
        IReadOnlyList<string> expectedJobKeys,
        CancellationToken cancellationToken)
    {
        var query = new JobCatalogQuery
        {
            SortField = sortField,
            SortDescending = descending,
            PageSize = 20
        };

        var definitions = await store.QueryActiveDefinitionsAsync(
            schedulerScopeKey,
            query,
            cancellationToken);
        var summaries = await store.QueryOperationalSummariesAsync(
            schedulerScopeKey,
            query,
            cancellationToken);

        definitions.Items.Select(static definition => definition.Declaration.JobKey)
            .Should().Equal(expectedJobKeys);
        summaries.Items.Select(static summary => summary.Definition.Declaration.JobKey)
            .Should().Equal(expectedJobKeys);
    }

    private static JobDeclaration CreateDeclaration(
        string jobKey,
        string jobName,
        JobType jobType,
        bool isDisabledByDefault) => new()
    {
        JobKey = jobKey,
        JobName = jobName,
        JobType = jobType,
        JobArgsKey = jobType == JobType.Triggered ? $"{jobKey}.Args" : null,
        CronExpression = jobType == JobType.Recurring ? "0 */5 * * * *" : null,
        TimeZoneId = jobType == JobType.Recurring ? TimeZoneInfo.Utc.Id : null,
        IsDisabledByDefault = isDisabledByDefault,
        MaxConcurrency = 1,
        MaxExecutionTimeout = TimeSpan.FromMinutes(5)
    };

    private sealed record CatalogOrderingExpectation(
        JobCatalogSortField SortField,
        IReadOnlyList<string> AscendingJobKeys,
        IReadOnlyList<string> DescendingJobKeys);
}
