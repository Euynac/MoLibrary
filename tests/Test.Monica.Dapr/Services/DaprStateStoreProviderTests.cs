using Dapr.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Dapr.Services;
using Monica.Modules;
using NSubstitute;
using Xunit;

namespace Test.Monica.Dapr.Services;

public sealed class DaprStateStoreProviderTests
{
    [Fact]
    public async Task TrySaveStateWithETagWithoutReadBackAsync_WhenSaveSucceeds_ShouldNotReadStateAgain()
    {
        var dapr = Substitute.For<DaprClient>();
        var value = new TestState("updated");
        dapr.TrySaveStateAsync(
                "test-state-store",
                "flight-key",
                value,
                "etag-1",
                Arg.Any<StateOptions>(),
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
            Arg.Any<StateOptions>(),
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
        dapr.TrySaveStateAsync(
                "test-state-store",
                "flight-key",
                value,
                "stale-etag",
                Arg.Any<StateOptions>(),
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

    private static DaprStateStoreProvider CreateProvider(DaprClient dapr)
    {
        return new DaprStateStoreProvider(
            dapr,
            NullLogger<DaprStateStoreProvider>.Instance,
            Options.Create(new ModuleDaprStateStoreOption
            {
                StateStoreName = "test-state-store"
            }));
    }

    private sealed record TestState(string Value);
}
