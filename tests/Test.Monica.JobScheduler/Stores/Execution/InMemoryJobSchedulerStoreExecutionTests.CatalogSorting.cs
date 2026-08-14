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
}
