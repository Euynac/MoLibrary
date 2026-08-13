using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Dapr.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core.JsonSerialization.Models;
using Monica.Core.JsonSerialization.Services;
using Monica.Dapr.Services;
using Monica.Modules;
using NSubstitute;
using Xunit;

namespace Test.Monica.Dapr.Services;

public sealed class DaprStateStoreProviderTests
{
    [Fact]
    public async Task SaveStateAsync_ShouldUseTypedSdkOperationAndPreserveTtlMetadata()
    {
        var dapr = Substitute.For<DaprClient>();
        var provider = CreateProvider(dapr);
        var value = new TestState("saved");

        await provider.SaveStateAsync(
            "state-key",
            value,
            TestContext.Current.CancellationToken,
            TimeSpan.FromSeconds(30));

        await dapr.Received(1).SaveStateAsync(
            "test-state-store",
            "state-key",
            value,
            null!,
            Arg.Is<IReadOnlyDictionary<string, string>>(metadata => metadata["ttlInSeconds"] == "30"),
            TestContext.Current.CancellationToken);
        await dapr.DidNotReceiveWithAnyArgs().SaveByteStateAsync(
            default!,
            default!,
            default,
            default!,
            default!,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetBulkStateAsync_ShouldUseTypedSdkOperationAndHonorEmptyFiltering()
    {
        var dapr = Substitute.For<DaprClient>();
        dapr.GetBulkStateAsync<TestState>(
                "test-state-store",
                Arg.Any<IReadOnlyList<string>>(),
                3,
                null!,
                Arg.Any<CancellationToken>())
            .Returns([
                new BulkStateItem<TestState>("found", new TestState("value"), "etag-1"),
                new BulkStateItem<TestState>("missing", null!, "etag-2")
            ]);
        var provider = CreateProvider(dapr, defaultBulkParallelism: 3);

        var results = await provider.GetBulkStateAsync<TestState>(
            ["found", "missing"],
            removeEmptyValue: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new TestState("value"), results["found"]);
        Assert.DoesNotContain("missing", results);
        await dapr.Received(1).GetBulkStateAsync<TestState>(
            "test-state-store",
            Arg.Is<IReadOnlyList<string>>(keys => keys.SequenceEqual(new[] { "found", "missing" })),
            3,
            null!,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetStateAsync_ShouldUseTypedSdkOperation()
    {
        var dapr = Substitute.For<DaprClient>();
        dapr.GetStateAsync<TestState>(
                "test-state-store",
                "state-key",
                null!,
                null!,
                Arg.Any<CancellationToken>())
            .Returns(new TestState("loaded"));
        var provider = CreateProvider(dapr);

        var result = await provider.GetStateAsync<TestState>(
            "state-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new TestState("loaded"), result);
        await dapr.Received(1).GetStateAsync<TestState>(
            "test-state-store",
            "state-key",
            null!,
            null!,
            TestContext.Current.CancellationToken);
        await dapr.DidNotReceiveWithAnyArgs().GetByteStateAsync(
            default!,
            default!,
            default!,
            default!,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TrySaveStateWithETagWithoutReadBackAsync_WhenSaveSucceeds_ShouldUseTypedSdkOperationWithoutReadBack()
    {
        var dapr = Substitute.For<DaprClient>();
        var value = new TestState("updated");
        dapr.TrySaveStateAsync(
                "test-state-store",
                "flight-key",
                value,
                "etag-1",
                null!,
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        var provider = CreateProvider(dapr);

        var success = await provider.TrySaveStateWithETagWithoutReadBackAsync(
            "flight-key",
            value,
            "etag-1",
            TestContext.Current.CancellationToken);

        Assert.True(success);
        await dapr.Received(1).TrySaveStateAsync(
            "test-state-store",
            "flight-key",
            value,
            "etag-1",
            null!,
            null!,
            TestContext.Current.CancellationToken);
        await dapr.DidNotReceiveWithAnyArgs().GetStateAndETagAsync<TestState>(
            default!,
            default!,
            default!,
            default!,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TrySaveStateWithETagWithoutReadBackAsync_WhenETagDoesNotMatch_ShouldReturnFalse()
    {
        var dapr = Substitute.For<DaprClient>();
        var value = new TestState("updated");
        dapr.TrySaveStateAsync(
                "test-state-store",
                "flight-key",
                value,
                "stale-etag",
                null!,
                null!,
                Arg.Any<CancellationToken>())
            .Returns(false);
        var provider = CreateProvider(dapr);

        var success = await provider.TrySaveStateWithETagWithoutReadBackAsync(
            "flight-key",
            value,
            "stale-etag",
            TestContext.Current.CancellationToken);

        Assert.False(success);
        await dapr.DidNotReceiveWithAnyArgs().GetStateAndETagAsync<TestState>(
            default!,
            default!,
            default!,
            default!,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TrySaveStateWithETagAsync_WhenSaveSucceeds_ShouldReadTheNewETagWithoutDeserializingState()
    {
        var dapr = Substitute.For<DaprClient>();
        var value = new TestState("updated");
        dapr.TrySaveStateAsync(
                "test-state-store",
                "flight-key",
                value,
                "etag-1",
                null!,
                null!,
                Arg.Any<CancellationToken>())
            .Returns(true);
        dapr.GetByteStateAndETagAsync(
                "test-state-store",
                "flight-key",
                null!,
                null!,
                Arg.Any<CancellationToken>())
            .Returns((ReadOnlyMemory<byte>.Empty, "etag-2"));
        var provider = CreateProvider(dapr);

        var result = await provider.TrySaveStateWithETagAsync(
            "flight-key",
            value,
            "etag-1",
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("etag-2", result.NewETag);
        await dapr.Received(1).GetByteStateAndETagAsync(
            "test-state-store",
            "flight-key",
            null!,
            null!,
            TestContext.Current.CancellationToken);
        await dapr.DidNotReceiveWithAnyArgs().GetStateAndETagAsync<TestState>(
            default!,
            default!,
            default!,
            default!,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TrySaveStateIfNotExistsAsync_WhenKeyExists_ShouldInspectOnlyTheETag()
    {
        var dapr = Substitute.For<DaprClient>();
        dapr.GetByteStateAndETagAsync(
                "test-state-store",
                "existing-key",
                null!,
                null!,
                Arg.Any<CancellationToken>())
            .Returns((new ReadOnlyMemory<byte>([0xFF]), "etag-existing"));
        var provider = CreateProvider(dapr);

        var success = await provider.TrySaveStateIfNotExistsAsync(
            "existing-key",
            new TestState("ignored"),
            TestContext.Current.CancellationToken);

        Assert.False(success);
        await dapr.DidNotReceiveWithAnyArgs().GetStateAndETagAsync<TestState>(
            default!,
            default!,
            default!,
            default!,
            TestContext.Current.CancellationToken);
        await dapr.DidNotReceiveWithAnyArgs().TrySaveStateAsync(
            default!,
            default!,
            default(TestState)!,
            default!,
            default!,
            default!,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SaveBulkStateAsync_ShouldUseTypedSdkBulkOperationAndPreserveTtlMetadata()
    {
        var dapr = Substitute.For<DaprClient>();
        var provider = CreateProvider(dapr);
        var first = new TestState("first");
        var second = new TestState("second");

        await provider.SaveBulkStateAsync(
            [("first-key", first), ("second-key", second)],
            TestContext.Current.CancellationToken,
            TimeSpan.FromSeconds(45));

        await dapr.Received(1).SaveBulkStateAsync(
            "test-state-store",
            Arg.Is<IReadOnlyList<SaveStateItem<TestState>>>(items =>
                items.Count == 2
                && items[0].Key == "first-key"
                && items[0].Value == first
                && items[0].ETag == null
                && items[0].Metadata!["ttlInSeconds"] == "45"
                && items[1].Key == "second-key"
                && items[1].Value == second
                && items[1].ETag == null
                && items[1].Metadata!["ttlInSeconds"] == "45"),
            TestContext.Current.CancellationToken);
        await dapr.DidNotReceiveWithAnyArgs().SaveByteStateAsync(
            default!,
            default!,
            default,
            default!,
            default!,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task QueryStateAsync_ShouldRenderWithCanonicalOptionsAndUseTypedSdkOperation()
    {
        var dapr = Substitute.For<DaprClient>();
        string? renderedQuery = null;
        dapr.QueryStateAsync<TestState>(
                "test-state-store",
                Arg.Do<string>(query => renderedQuery = query),
                null!,
                Arg.Any<CancellationToken>())
            .Returns(new StateQueryResponse<TestState>(
                [new StateQueryItem<TestState>("found", new TestState("matched"), "etag", string.Empty)],
                string.Empty,
                new Dictionary<string, string>()));
        var provider = CreateProvider(dapr);

        var results = await provider.QueryStateAsync<TestState>(
            builder => builder.Where(filter => filter.Eq(state => state.Value, "match")).Build(),
            TestContext.Current.CancellationToken);

        Assert.Equal(new TestState("matched"), results["found"]);
        var query = Assert.IsType<string>(renderedQuery);
        Assert.Contains("\"EQ\":{\"value\":\"match\"}", query, StringComparison.Ordinal);
        await dapr.Received(1).QueryStateAsync<TestState>(
            "test-state-store",
            query,
            null!,
            TestContext.Current.CancellationToken);
    }

    private static DaprStateStoreProvider CreateProvider(
        DaprClient dapr,
        int? defaultBulkParallelism = null)
    {
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        serializerOptions.MakeReadOnly();

        return new DaprStateStoreProvider(
            dapr,
            NullLogger<DaprStateStoreProvider>.Instance,
            Options.Create(new ModuleDaprStateStoreOption
            {
                StateStoreName = "test-state-store",
                DefaultBulkParallelism = defaultBulkParallelism
            }),
            new JsonSerializerOptionsProvider(serializerOptions, DateTimeWireFormat.Iso8601WallClock));
    }

    public sealed record TestState(string Value);
}
