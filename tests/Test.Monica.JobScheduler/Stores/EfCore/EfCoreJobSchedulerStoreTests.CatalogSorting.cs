using Test.Monica.JobScheduler.Stores.Catalog;
using Xunit;

namespace Test.Monica.JobScheduler.Stores.EfCore;

public sealed partial class EfCoreJobSchedulerStoreTests
{
    [Fact]
    public async Task CatalogQueries_WhenSortingRequested_ShouldApplyProviderIndependentStableOrdering()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        await JobCatalogSortingContract.SeedAsync(
            fixture.Store,
            fixture.Scope,
            TestContext.Current.CancellationToken);

        await JobCatalogSortingContract.AssertSupportedOrderingAsync(
            fixture.Store,
            fixture.Scope,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CatalogQueries_WhenSortFieldIsInvalid_ShouldRejectBothProjections()
    {
        await using var fixture = await StoreFixture.CreateAsync(
            START_TIME,
            TestContext.Current.CancellationToken);
        await JobCatalogSortingContract.SeedAsync(
            fixture.Store,
            fixture.Scope,
            TestContext.Current.CancellationToken);

        await JobCatalogSortingContract.AssertInvalidSortRejectedAsync(
            fixture.Store,
            fixture.Scope,
            TestContext.Current.CancellationToken);
    }
}
