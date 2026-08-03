using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Abstractions;
using Xunit;

namespace Test.Monica.Configuration.EfCore;

public sealed partial class DatabaseConfigurationStorePublishTests
{
    [Fact]
    public async Task GetDefinitionPublisherStatesAsync_WhenSeveralDefinitionsAreRequested_ShouldUseOneBoundedQuery()
    {
        var databasePath = await CreateMigratedDatabaseAsync();
        var interceptor = new MetadataQueryInterceptor();
        await using var provider = CreateProvider(databasePath, interceptor);
        var store = provider.GetRequiredService<IConfigurationMetadataStore>();
        var first = CreateDefinition("Test.Publishers.Read.First");
        var second = CreateDefinition("Test.Publishers.Read.Second");
        var publisher = CreatePublisher("Service.Reader", "reader:1");
        await store.PublishAsync(
            CreatePublication(publisher, [first, second]),
            TestContext.Current.CancellationToken);
        interceptor.Clear();

        var states = await store.GetDefinitionPublisherStatesAsync(
            [first.DefinitionKey, second.DefinitionKey, "Test.Publishers.Read.Missing"],
            TestContext.Current.CancellationToken);

        interceptor.PublisherStateQueryCount.Should().Be(1);
        states[first.DefinitionKey].Should().ContainSingle(state =>
            state.PublisherKey == publisher.PublisherKey);
        states[second.DefinitionKey].Should().ContainSingle(state =>
            state.PublisherKey == publisher.PublisherKey);
        states["Test.Publishers.Read.Missing"].Should().BeEmpty();
    }
}
