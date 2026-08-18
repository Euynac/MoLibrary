using AwesomeAssertions;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Providers;
using Test.Monica.JobScheduler.Stores.Catalog;
using Xunit;

namespace Test.Monica.JobScheduler.Stores.Execution;

public sealed partial class InMemoryJobSchedulerStoreExecutionTests
{
    [Fact]
    public async Task CatalogQueries_WhenSortingRequested_ShouldApplyProviderIndependentStableOrdering()
    {
        var store = new InMemoryJobSchedulerStore(new ManualTimeProvider(START_TIME));
        await JobCatalogSortingContract.SeedAsync(
            store,
            SCOPE,
            TestContext.Current.CancellationToken);

        await JobCatalogSortingContract.AssertSupportedOrderingAsync(
            store,
            SCOPE,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CatalogQueries_WhenSortFieldIsInvalid_ShouldRejectBothProjections()
    {
        var store = new InMemoryJobSchedulerStore(new ManualTimeProvider(START_TIME));
        await JobCatalogSortingContract.SeedAsync(
            store,
            SCOPE,
            TestContext.Current.CancellationToken);

        await JobCatalogSortingContract.AssertInvalidSortRejectedAsync(
            store,
            SCOPE,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CatalogQueries_WhenDisplayPolicyChanges_ShouldSearchAndSortByEffectivePresentation()
    {
        var store = new InMemoryJobSchedulerStore(new ManualTimeProvider(START_TIME));
        await JobCatalogSortingContract.SeedAsync(
            store,
            SCOPE,
            TestContext.Current.CancellationToken);
        var definition = (await store.GetActiveDefinitionAsync(
            SCOPE,
            "jobs.delta",
            TestContext.Current.CancellationToken))!;
        await store.UpdatePolicyAsync(
            SCOPE,
            definition.OwnerId,
            definition.Declaration.JobKey,
            new JobPolicyChange
            {
                Overrides = definition.Policy.Overrides with
                {
                    DisplayNameOverride = "Aardvark operator",
                    DescriptionOverride = new JobDescriptionOverride { Value = "maintenance-needle" }
                },
                ExpectedConcurrencyStamp = definition.Policy.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        var sorted = await store.QueryActiveDefinitionsAsync(
            SCOPE,
            new JobCatalogQuery { SortField = JobCatalogSortField.JobName, PageSize = 20 },
            TestContext.Current.CancellationToken);
        var searched = await store.QueryOperationalSummariesAsync(
            SCOPE,
            new JobCatalogQuery { SearchText = "maintenance-needle", PageSize = 20 },
            TestContext.Current.CancellationToken);

        sorted.Items.Select(static item => item.Declaration.JobKey).Should().Equal(
            "jobs.delta",
            "jobs.charlie",
            "jobs.alpha",
            "jobs.bravo");
        searched.Items.Should().ContainSingle()
            .Which.Definition.Declaration.JobKey.Should().Be("jobs.delta");
    }
}
