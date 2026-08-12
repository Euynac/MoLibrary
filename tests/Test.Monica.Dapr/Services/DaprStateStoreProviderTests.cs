using System.Text.Json;
using Dapr.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Dapr.Services;
using Monica.Dapr.Services.Support;
using Monica.Modules;
using Monica.StateStore.Abstractions;
using NSubstitute;
using Xunit;

namespace Test.Monica.Dapr.Services;

public sealed class DaprStateStoreProviderTests
{
    private static readonly StateDocumentProfile TestDocumentProfile = StateDocumentProfile.CreateJson(
        ModuleStateStoreOption.DURABLE_JSON_PROFILE,
        "test");

    [Fact]
    public async Task TrySaveStateWithETagWithoutReadBackAsync_WhenSaveSucceeds_ShouldNotReadStateAgain()
    {
        var dapr = Substitute.For<DaprClient>();
        var value = new TestState("updated");
        dapr.TrySaveByteStateAsync(
                "test-state-store",
                "flight-key",
                Arg.Is<ReadOnlyMemory<byte>>(bytes => MatchesSerializedState(bytes, value)),
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
        await dapr.Received(1).TrySaveByteStateAsync(
            "test-state-store",
            "flight-key",
            Arg.Is<ReadOnlyMemory<byte>>(bytes => MatchesSerializedState(bytes, value)),
            "etag-1",
            null!,
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            TestContext.Current.CancellationToken);
        await dapr.DidNotReceiveWithAnyArgs()
            .GetStateAndETagAsync<TestState>(
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
        dapr.TrySaveByteStateAsync(
                "test-state-store",
                "flight-key",
                Arg.Any<ReadOnlyMemory<byte>>(),
                "stale-etag",
                null!,
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(false);
        var provider = CreateProvider(dapr);

        var success = await provider.TrySaveStateWithETagWithoutReadBackAsync(
            "flight-key",
            value,
            "stale-etag",
            TestContext.Current.CancellationToken);

        Assert.False(success);
        await dapr.DidNotReceiveWithAnyArgs()
            .GetStateAndETagAsync<TestState>(
                default!,
                default!,
                default!,
                default!,
                TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SaveBulkStateAsync_ShouldUseProfileSerializedByteWrites()
    {
        var profile = StateDocumentProfile.CreateJson(
            ModuleStateStoreOption.DURABLE_JSON_PROFILE,
            "bulk-test",
            options =>
            {
                options.PropertyNamingPolicy = null;
                options.WriteIndented = true;
            });
        var dapr = Substitute.For<DaprClient>();
        var first = new TestState("first");
        var second = new TestState("second");
        var provider = CreateProvider(dapr, profile);

        await provider.SaveBulkStateAsync(
            [("first-key", first), ("second-key", second)],
            TestContext.Current.CancellationToken,
            TimeSpan.FromSeconds(45));

        await dapr.Received(1).SaveByteStateAsync(
            "test-state-store",
            "first-key",
            Arg.Is<ReadOnlyMemory<byte>>(bytes => MatchesSerializedState(profile, bytes, first)),
            null!,
            Arg.Is<IReadOnlyDictionary<string, string>>(metadata => metadata["ttlInSeconds"] == "45"),
            TestContext.Current.CancellationToken);
        await dapr.Received(1).SaveByteStateAsync(
            "test-state-store",
            "second-key",
            Arg.Is<ReadOnlyMemory<byte>>(bytes => MatchesSerializedState(profile, bytes, second)),
            null!,
            Arg.Is<IReadOnlyDictionary<string, string>>(metadata => metadata["ttlInSeconds"] == "45"),
            TestContext.Current.CancellationToken);
        await dapr.DidNotReceiveWithAnyArgs().SaveBulkStateAsync<JsonElement>(
            default!,
            default!,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task QueryStateAsync_ShouldDeserializeRawQueryDocumentsWithTheSelectedProfile()
    {
        var dapr = Substitute.For<DaprClient>();
        var queryClient = new TestStateQueryClient(
            new Dictionary<string, string?>
            {
                ["found"] = "{\"value\":\"from-profile\"}",
                ["missing"] = null
            });
        var provider = CreateProvider(dapr, stateQueryClient: queryClient);

        var results = await provider.QueryStateAsync<TestState>(
            builder => builder.Where(filter => filter.Eq(state => state.Value, "match")).Build(),
            TestContext.Current.CancellationToken);

        Assert.Equal(new TestState("from-profile"), results["found"]);
        Assert.Null(results["missing"]);
        Assert.Equal("test-state-store", queryClient.StateStoreName);
        Assert.NotNull(queryClient.JsonQuery);
        Assert.Contains("\"EQ\"", queryClient.JsonQuery, StringComparison.Ordinal);
        await dapr.DidNotReceiveWithAnyArgs().QueryStateAsync<JsonElement>(
            default!,
            default!,
            default!,
            TestContext.Current.CancellationToken);
    }

    private static DaprStateStoreProvider CreateProvider(
        DaprClient dapr,
        StateDocumentProfile? documentProfile = null,
        IDaprStateQueryClient? stateQueryClient = null)
    {
        return new DaprStateStoreProvider(
            dapr,
            NullLogger<DaprStateStoreProvider>.Instance,
            Options.Create(new ModuleDaprStateStoreOption
            {
                StateStoreName = "test-state-store"
            }),
            new TestDocumentProfileProvider(documentProfile ?? TestDocumentProfile),
            stateQueryClient ?? new TestStateQueryClient(new Dictionary<string, string?>()));
    }

    private sealed record TestState(string Value);

    private static bool MatchesSerializedState(ReadOnlyMemory<byte> bytes, TestState expected)
    {
        return TestDocumentProfile.Deserialize<TestState>(bytes.Span) == expected;
    }

    private static bool MatchesSerializedState(
        StateDocumentProfile profile,
        ReadOnlyMemory<byte> bytes,
        TestState expected)
    {
        return bytes.Span.SequenceEqual(profile.SerializeToUtf8Bytes(expected));
    }

    private sealed class TestDocumentProfileProvider(StateDocumentProfile profile) : IStateDocumentProfileProvider
    {
        public IReadOnlyDictionary<string, StateDocumentProfile> Profiles =>
            new Dictionary<string, StateDocumentProfile>
            {
                [profile.Name] = profile
            };

        public StateDocumentProfile GetRequiredProfile(string name)
        {
            Assert.Equal(profile.Name, name);
            return profile;
        }
    }

    private sealed class TestStateQueryClient(IReadOnlyDictionary<string, string?> results)
        : IDaprStateQueryClient
    {
        public string? StateStoreName { get; private set; }

        public string? JsonQuery { get; private set; }

        public Task<IReadOnlyDictionary<string, string?>> QueryAsync(
            string stateStoreName,
            string jsonQuery,
            CancellationToken cancellationToken)
        {
            StateStoreName = stateStoreName;
            JsonQuery = jsonQuery;
            return Task.FromResult(results);
        }
    }
}
