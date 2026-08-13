using AwesomeAssertions;
using Microsoft.JSInterop;
using Monica.UI.Shell.Support;
using Xunit;

namespace Test.Monica.UI.Shell.Support;

public sealed class JsModuleSessionTests
{
    [Fact]
    public async Task InvokeAsync_WhenCallsOverlap_ShouldImportModuleOnce()
    {
        var module = new CountingJsModule();
        var runtime = new ImmediateJsRuntime(module);
        await using var session = new JsModuleSession(runtime, "./module.js");

        await Task.WhenAll(
            session.InvokeAsync<int>("read").AsTask(),
            session.InvokeAsync<int>("read").AsTask(),
            session.InvokeAsync<int>("read").AsTask());

        runtime.ImportCount.Should().Be(1);
        module.InvocationCount.Should().Be(3);
    }

    [Fact]
    public async Task DisposeAsync_WhenImportCompletesLate_ShouldDisposeLateReference()
    {
        var module = new CountingJsModule();
        var runtime = new DelayedJsRuntime();
        var session = new JsModuleSession(runtime, "./module.js");

        var invocation = session.InvokeAsync<int>("read").AsTask();
        await runtime.ImportStarted;
        var disposal = session.DisposeAsync().AsTask();

        runtime.CompleteImport(module);

        await Assert.ThrowsAsync<ObjectDisposedException>(() => invocation);
        await disposal;
        module.DisposeCount.Should().Be(1);
        module.InvocationCount.Should().Be(0);
    }

    private sealed class ImmediateJsRuntime(IJSObjectReference module) : IJSRuntime
    {
        public int ImportCount { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            identifier.Should().Be("import");
            ImportCount++;
            return ValueTask.FromResult((TValue)module);
        }
    }

    private sealed class DelayedJsRuntime : IJSRuntime
    {
        private readonly TaskCompletionSource _importStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<IJSObjectReference> _module = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ImportStarted => _importStarted.Task;

        public void CompleteImport(IJSObjectReference module) => _module.SetResult(module);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            identifier.Should().Be("import");
            _importStarted.SetResult();
            return new ValueTask<TValue>(CompleteAsync<TValue>());
        }

        private async Task<TValue> CompleteAsync<TValue>() => (TValue)await _module.Task;
    }

    private sealed class CountingJsModule : IJSObjectReference
    {
        public int InvocationCount { get; private set; }
        public int DisposeCount { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            InvocationCount++;
            return ValueTask.FromResult((TValue)(object)1);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
