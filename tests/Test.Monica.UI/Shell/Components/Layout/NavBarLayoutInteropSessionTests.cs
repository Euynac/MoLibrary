using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Monica.UI.Shell.Components.Layout;
using Test.Monica.UI.UIModuleSystem;
using Xunit;

namespace Test.Monica.UI.Shell.Components.Layout;

public sealed class NavBarLayoutInteropSessionTests
{
    [Fact]
    public async Task Concurrent_initialization_imports_and_creates_one_observer()
    {
        var importStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var importCompletion = new TaskCompletionSource<IJSObjectReference>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new ControlledJsReference();
        var module = CreateModule(observer);
        var runtime = new ControlledJsRuntime(() =>
        {
            importStarted.TrySetResult(true);
            return new ValueTask<IJSObjectReference>(importCompletion.Task);
        });
        await using var session = CreateSession(runtime);

        var firstInitialization = InitializeAsync(session);
        await importStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        var secondInitialization = InitializeAsync(session);
        importCompletion.SetResult(module);
        await Task.WhenAll(firstInitialization, secondInitialization);

        runtime.ImportCalls.Should().Be(1);
        module.InvocationIdentifiers.Should().ContainSingle()
            .Which.Should().Be("createNavBarLayoutObserver");
    }

    [Fact]
    public async Task Dispose_during_late_import_releases_module_without_creating_observer()
    {
        var importStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var importCompletion = new TaskCompletionSource<IJSObjectReference>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var module = new ControlledJsReference();
        var runtime = new ControlledJsRuntime(() =>
        {
            importStarted.TrySetResult(true);
            return new ValueTask<IJSObjectReference>(importCompletion.Task);
        });
        var session = CreateSession(runtime);

        var initialization = InitializeAsync(session);
        await importStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        var disposal = session.DisposeAsync().AsTask();
        importCompletion.SetResult(module);
        await Task.WhenAll(initialization, disposal);

        module.InvocationIdentifiers.Should().BeEmpty();
        module.DisposeCalls.Should().Be(1);
    }

    [Fact]
    public async Task Dispose_during_late_observer_creation_releases_every_late_reference()
    {
        var observerCreationStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var observerCreationCompletion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new ControlledJsReference();
        DotNetObjectReference<NavBar>? callbackReference = null;
        var module = new ControlledJsReference((identifier, arguments) =>
        {
            identifier.Should().Be("createNavBarLayoutObserver");
            callbackReference = (DotNetObjectReference<NavBar>)arguments![4]!;
            observerCreationStarted.TrySetResult(true);
            return new ValueTask<object?>(observerCreationCompletion.Task);
        });
        var runtime = new ControlledJsRuntime(() => ValueTask.FromResult<IJSObjectReference>(module));
        var session = CreateSession(runtime);

        var initialization = InitializeAsync(session);
        await observerCreationStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        var disposal = session.DisposeAsync().AsTask();
        observerCreationCompletion.SetResult(observer);
        await Task.WhenAll(initialization, disposal);

        observer.InvocationIdentifiers.Should().ContainSingle().Which.Should().Be("dispose");
        observer.DisposeCalls.Should().Be(1);
        module.DisposeCalls.Should().Be(1);
        callbackReference.Should().NotBeNull();
        Action readDisposedCallback = () => _ = callbackReference!.Value;
        readDisposedCallback.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Concurrent_repeated_disposal_waits_for_one_complete_cleanup()
    {
        var observer = new ControlledJsReference();
        var module = CreateModule(observer);
        var runtime = new ControlledJsRuntime(() => ValueTask.FromResult<IJSObjectReference>(module));
        var session = CreateSession(runtime);
        await InitializeAsync(session);

        var disposals = Enumerable.Range(0, 8)
            .Select(_ => session.DisposeAsync().AsTask())
            .ToArray();
        await Task.WhenAll(disposals);

        observer.InvocationIdentifiers.Should().ContainSingle().Which.Should().Be("dispose");
        observer.DisposeCalls.Should().Be(1);
        module.DisposeCalls.Should().Be(1);
    }

    [Fact]
    public async Task Circuit_loss_during_cleanup_does_not_interrupt_session_disposal()
    {
        var observer = new ControlledJsReference(
            invoke: (_, _) => ValueTask.FromException<object?>(
                new JSDisconnectedException("circuit disconnected")),
            dispose: () => ValueTask.FromException(
                new JSDisconnectedException("circuit disconnected")));
        var module = CreateModule(
            observer,
            () => ValueTask.FromException(
                new JSDisconnectedException("circuit disconnected")));
        var runtime = new ControlledJsRuntime(() => ValueTask.FromResult<IJSObjectReference>(module));
        var session = CreateSession(runtime);
        await InitializeAsync(session);

        Func<Task> dispose = () => session.DisposeAsync().AsTask();

        await dispose.Should().NotThrowAsync();
        observer.InvocationIdentifiers.Should().ContainSingle().Which.Should().Be("dispose");
        observer.DisposeCalls.Should().Be(1);
        module.DisposeCalls.Should().Be(1);
    }

    private static NavBarLayoutInteropSession CreateSession(IJSRuntime runtime) =>
        new(runtime, new NavBar());

    private static Task InitializeAsync(NavBarLayoutInteropSession session) =>
        session.InitializeAsync(default(ElementReference), default, 6, 8);

    private static ControlledJsReference CreateModule(
        IJSObjectReference observer,
        Func<ValueTask>? dispose = null) => new(
        (identifier, _) =>
        {
            identifier.Should().Be("createNavBarLayoutObserver");
            return ValueTask.FromResult<object?>(observer);
        },
        dispose);
}
