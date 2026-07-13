using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Monica.UI.Shell.Support;
using Xunit;

namespace Test.Monica.UI.Shell.Support;

public sealed class BrowserStorageTests
{
    [Fact]
    public async Task TrySetAsync_WhenWriteSucceeds_ShouldReturnSuccessAndPrefixKey()
    {
        var module = new TestJsModule();
        await using var storage = CreateStorage(module);

        var result = await storage.TrySetAsync("chat:catalog", new StoredValue("value"));

        result.Succeeded.Should().BeTrue();
        result.FailureKind.Should().Be(BrowserStorageWriteFailureKind.None);
        module.LastIdentifier.Should().Be("trySetItem");
        module.LastArguments.Should().Equal("local", "mo:chat:catalog", "{\"name\":\"value\"}");
    }

    [Fact]
    public async Task TrySetAsync_WhenBrowserReportsQuotaExceeded_ShouldReturnQuotaFailure()
    {
        var module = new TestJsModule("quota-exceeded");
        await using var storage = CreateStorage(module);

        var result = await storage.TrySetAsync("chat:catalog", new StoredValue("value"));

        result.Succeeded.Should().BeFalse();
        result.FailureKind.Should().Be(BrowserStorageWriteFailureKind.QuotaExceeded);
    }

    [Fact]
    public async Task TrySetAsync_WhenCircuitDisconnects_ShouldReturnUnavailableFailure()
    {
        var module = new TestJsModule(exception: new JSDisconnectedException("Circuit disconnected."));
        await using var storage = CreateStorage(module);

        var result = await storage.TrySetAsync("chat:catalog", new StoredValue("value"));

        result.Succeeded.Should().BeFalse();
        result.FailureKind.Should().Be(BrowserStorageWriteFailureKind.StorageUnavailable);
    }

    [Fact]
    public async Task TrySetAsync_WhenSerializationFails_ShouldReturnSerializationFailureWithoutInterop()
    {
        var module = new TestJsModule();
        await using var storage = CreateStorage(module);
        var value = new CyclicValue();
        value.Self = value;

        var result = await storage.TrySetAsync("chat:catalog", value);

        result.Succeeded.Should().BeFalse();
        result.FailureKind.Should().Be(BrowserStorageWriteFailureKind.SerializationFailed);
        module.InvocationCount.Should().Be(0);
    }

    [Fact]
    public async Task SetAsync_WhenWriteFails_ShouldPreserveSilentCompatibilityBehavior()
    {
        var module = new TestJsModule("quota-exceeded");
        await using var storage = CreateStorage(module);

        var action = () => storage.SetAsync("chat:catalog", new StoredValue("value"));

        await action.Should().NotThrowAsync();
        module.InvocationCount.Should().Be(1);
    }

    private static BrowserStorage CreateStorage(TestJsModule module)
        => new(new TestJsRuntime(module), NullLogger<BrowserStorage>.Instance);

    private sealed record StoredValue(string Name);

    private sealed class CyclicValue
    {
        public CyclicValue? Self { get; set; }
    }

    private sealed class TestJsRuntime(IJSObjectReference module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            identifier.Should().Be("import");
            return ValueTask.FromResult((TValue)module);
        }
    }

    private sealed class TestJsModule(string? failureCode = null, Exception? exception = null) : IJSObjectReference
    {
        public int InvocationCount { get; private set; }
        public string? LastIdentifier { get; private set; }
        public object?[] LastArguments { get; private set; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            InvocationCount++;
            LastIdentifier = identifier;
            LastArguments = args ?? [];

            if (exception is not null)
            {
                return ValueTask.FromException<TValue>(exception);
            }

            return ValueTask.FromResult((TValue)(object?)failureCode!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
