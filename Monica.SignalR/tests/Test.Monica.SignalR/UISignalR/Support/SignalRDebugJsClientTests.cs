using System.Text.Json;
using AwesomeAssertions;
using Microsoft.JSInterop;
using Monica.SignalR.UISignalR.Support;
using Xunit;

namespace Test.Monica.SignalR.UISignalR.Support;

public sealed class SignalRDebugJsClientTests
{
    [Fact]
    public async Task DisposeOwnedSessionAsync_WhenDrainCompletes_ShouldReleaseCallbackAndInteropHandles()
    {
        var shutdown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new TestJsObjectReference(shutdown.Task);
        var module = new TestJsObjectReference();
        var callbackReleased = false;

        var disposal = SignalRDebugJsClient.DisposeOwnedSessionAsync(
            session,
            () => callbackReleased = true,
            module,
            TimeSpan.FromSeconds(1));

        callbackReleased.Should().BeFalse();
        shutdown.SetResult();
        await disposal;

        callbackReleased.Should().BeTrue();
        session.IsDisposed.Should().BeTrue();
        module.IsDisposed.Should().BeTrue();
        session.ShutdownCancellationToken.Should().Be(CancellationToken.None);
    }

    [Fact]
    public async Task DisposeOwnedSessionAsync_WhenBoundedWaitExpires_ShouldDeferCallbackReleaseUntilDrainCompletes()
    {
        var shutdown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new TestJsObjectReference(shutdown.Task);
        var module = new TestJsObjectReference();

        await SignalRDebugJsClient.DisposeOwnedSessionAsync(
            session,
            callbackReleased.SetResult,
            module,
            TimeSpan.Zero);

        callbackReleased.Task.IsCompleted.Should().BeFalse();
        session.IsDisposed.Should().BeTrue();
        module.IsDisposed.Should().BeTrue();

        shutdown.SetResult();
        await callbackReleased.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DisposeOwnedSessionAsync_WhenCircuitDisconnects_ShouldReleaseCallbackAndInteropHandles()
    {
        var session = new TestJsObjectReference(Task.FromException(new JSDisconnectedException("Circuit closed.")));
        var module = new TestJsObjectReference();
        var callbackReleased = false;

        await SignalRDebugJsClient.DisposeOwnedSessionAsync(
            session,
            () => callbackReleased = true,
            module,
            TimeSpan.FromSeconds(1));

        callbackReleased.Should().BeTrue();
        session.IsDisposed.Should().BeTrue();
        module.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeOwnedSessionAsync_WhenStopFailsAfterDrain_ShouldReleaseCallbackWithoutThrowing()
    {
        var session = new TestJsObjectReference(
            result: JsonSerializer.SerializeToElement(new { drained = true, error = "stop failed" }));
        var module = new TestJsObjectReference();
        var callbackReleased = false;

        Func<Task> disposal = async () => await SignalRDebugJsClient.DisposeOwnedSessionAsync(
            session,
            () => callbackReleased = true,
            module,
            TimeSpan.FromSeconds(1));

        await disposal.Should().NotThrowAsync();
        callbackReleased.Should().BeTrue();
        session.IsDisposed.Should().BeTrue();
        module.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeOwnedSessionAsync_WhenShutdownIsCanceled_ShouldRetainCallbackReference()
    {
        var session = new TestJsObjectReference(Task.FromCanceled(new CancellationToken(canceled: true)));
        var module = new TestJsObjectReference();
        var callbackReleased = false;

        await SignalRDebugJsClient.DisposeOwnedSessionAsync(
            session,
            () => callbackReleased = true,
            module,
            TimeSpan.FromSeconds(1));

        callbackReleased.Should().BeFalse();
        session.IsDisposed.Should().BeTrue();
        module.IsDisposed.Should().BeTrue();
    }

    private sealed class TestJsObjectReference(
        Task? shutdown = null,
        JsonElement? result = null) : IJSObjectReference
    {
        public bool IsDisposed { get; private set; }

        public CancellationToken ShutdownCancellationToken { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public async ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            identifier.Should().Be("shutdown");
            ShutdownCancellationToken = cancellationToken;
            if (shutdown is not null)
            {
                await shutdown;
            }

            var shutdownResult = result
                ?? JsonSerializer.SerializeToElement(new { drained = true, error = (string?)null });
            return (TValue)(object)shutdownResult;
        }
    }
}
