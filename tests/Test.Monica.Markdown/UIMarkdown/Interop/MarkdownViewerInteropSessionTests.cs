using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Monica.Markdown.UIMarkdown.Interop;
using Test.Monica.Markdown.Support;

namespace Test.Monica.Markdown.UIMarkdown.Interop;

public sealed class MarkdownViewerInteropSessionTests
{
    private const string MARKDOWN_LAYOUT_MODULE_PATH =
        "./_content/Monica.Markdown/js/markdown-layout.js";

    [Fact]
    public async Task InitializeAsync_WhenDisposedDuringModuleImport_ShouldDisposeLateModuleWithoutCreatingSession()
    {
        var eventLog = new JsInteropEventLog();
        var import = new ControlledJsOperation(eventLog, "runtime.import");
        var module = new ControlledJsObjectReference(eventLog, "module");
        var runtime = new ControlledJsRuntime(eventLog, import, MARKDOWN_LAYOUT_MODULE_PATH);
        var session = new MarkdownViewerInteropSession(runtime, _ => Task.CompletedTask);

        var initialization = session.InitializeAsync(default);
        await import.Started.WaitAsync(TestContext.Current.CancellationToken);

        var disposal = session.DisposeAsync().AsTask();
        disposal.IsCompleted.Should().BeFalse();
        import.Complete(module);

        await AwaitExpectedCancellationAsync(initialization);
        await disposal;

        module.Invocations.Should().BeEmpty();
        module.DisposeCount.Should().Be(1);
        eventLog.Events.Should().ContainInOrder(
            "runtime.import:completed",
            "module.dispose");
    }

    [Fact]
    public async Task InitializeAsync_WhenDisposedDuringSessionCreation_ShouldShutdownAndDisposeLateSession()
    {
        var fixture = new InteropFixture();
        fixture.Import.Complete(fixture.Module);
        var shouldShowSidebar = fixture.Module.Plan("shouldShowSidebarByDefault");
        shouldShowSidebar.Complete(true);
        var createSession = fixture.Module.Plan("createMarkdownViewerSession");
        var shutdown = fixture.ViewerSession.Plan("shutdown");
        shutdown.Complete();

        var initialization = fixture.Session.InitializeAsync(default);
        await createSession.Started.WaitAsync(TestContext.Current.CancellationToken);

        var disposal = fixture.Session.DisposeAsync().AsTask();
        disposal.IsCompleted.Should().BeFalse();
        createSession.Complete(fixture.ViewerSession);

        await AwaitExpectedCancellationAsync(initialization);
        await disposal;

        fixture.ViewerSession.Invocations.Should().ContainSingle(invocation =>
            invocation.Identifier == "shutdown");
        fixture.ViewerSession.DisposeCount.Should().Be(1);
        fixture.Module.DisposeCount.Should().Be(1);
        fixture.EventLog.Events.Should().ContainInOrder(
            "module.createMarkdownViewerSession:completed",
            "viewer.shutdown:started",
            "viewer.shutdown:completed",
            "viewer.dispose",
            "module.dispose");
    }

    [Fact]
    public async Task InitializeAsync_WhenCircuitDisconnectsDuringSessionCreation_ShouldReleaseModuleAndReturnNull()
    {
        var fixture = new InteropFixture();
        fixture.Import.Complete(fixture.Module);
        var shouldShowSidebar = fixture.Module.Plan("shouldShowSidebarByDefault");
        shouldShowSidebar.Complete(true);
        var createSession = fixture.Module.Plan("createMarkdownViewerSession");
        createSession.Fail(new JSDisconnectedException("Circuit disconnected."));

        var result = await fixture.Session.InitializeAsync(default);

        result.Should().BeNull();
        fixture.Module.DisposeCount.Should().Be(1);
        fixture.ViewerSession.DisposeCount.Should().Be(0);
        await fixture.Session.DisposeAsync();
        fixture.Module.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ClearSearchHitAsync_WhenDisposedDuringInvocation_ShouldDrainInvocationBeforeTeardown()
    {
        var fixture = await InteropFixture.CreateInitializedAsync();
        var clearSearchHit = fixture.ViewerSession.Plan("clearSearchHit");
        var shutdown = fixture.ViewerSession.Plan("shutdown");
        shutdown.Complete();

        var invocation = fixture.Session.ClearSearchHitAsync();
        await clearSearchHit.Started.WaitAsync(TestContext.Current.CancellationToken);

        var disposal = fixture.Session.DisposeAsync().AsTask();
        disposal.IsCompleted.Should().BeFalse();
        fixture.ViewerSession.DisposeCount.Should().Be(0);
        clearSearchHit.Complete();

        await AwaitExpectedCancellationAsync(invocation);
        await disposal;

        fixture.ViewerSession.DisposeCount.Should().Be(1);
        fixture.Module.DisposeCount.Should().Be(1);
        fixture.EventLog.Events.Should().ContainInOrder(
            "viewer.clearSearchHit:completed",
            "viewer.shutdown:started",
            "viewer.shutdown:completed",
            "viewer.dispose",
            "module.dispose");
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledConcurrently_ShouldTeardownExactlyOnce()
    {
        var fixture = await InteropFixture.CreateInitializedAsync();
        var shutdown = fixture.ViewerSession.Plan("shutdown");
        shutdown.Complete();

        var disposals = Enumerable.Range(0, 8)
            .Select(_ => fixture.Session.DisposeAsync().AsTask())
            .ToArray();

        await Task.WhenAll(disposals);

        fixture.ViewerSession.Invocations.Should().ContainSingle(invocation =>
            invocation.Identifier == "shutdown");
        fixture.ViewerSession.DisposeCount.Should().Be(1);
        fixture.Module.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_WhenShutdownFails_ShouldReleaseReferencesAndPropagateFailure()
    {
        var fixture = await InteropFixture.CreateInitializedAsync();
        var shutdown = fixture.ViewerSession.Plan("shutdown");
        shutdown.Fail(new JSException("Viewer shutdown failed."));

        var action = () => fixture.Session.DisposeAsync().AsTask();

        await action.Should().ThrowAsync<JSException>()
            .WithMessage("Viewer shutdown failed.");
        fixture.ViewerSession.DisposeCount.Should().Be(1);
        fixture.Module.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task Calls_WhenDisposalCompleted_ShouldNotUseDisposedReferences()
    {
        var fixture = await InteropFixture.CreateInitializedAsync();
        var shutdown = fixture.ViewerSession.Plan("shutdown");
        shutdown.Complete();
        await fixture.Session.DisposeAsync();
        var runtimeInvocationCount = fixture.Runtime.InvocationCount;
        var viewerInvocationCount = fixture.ViewerSession.Invocations.Count;

        var initializeResult = await fixture.Session.InitializeAsync(default(ElementReference));
        await fixture.Session.ClearSearchHitAsync();
        await fixture.Session.RefreshHeadingTrackerAsync([], null);

        initializeResult.Should().BeNull();
        fixture.Runtime.InvocationCount.Should().Be(runtimeInvocationCount);
        fixture.ViewerSession.Invocations.Should().HaveCount(viewerInvocationCount);
        fixture.ViewerSession.DisposeCount.Should().Be(1);
        fixture.Module.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task OnHashChangedAsync_WhenDisposed_ShouldIgnoreLateCallback()
    {
        var callbackCount = 0;
        var fixture = await InteropFixture.CreateInitializedAsync(_ =>
        {
            callbackCount++;
            return Task.CompletedTask;
        });
        var callbackReference = fixture.Module.Invocations
            .Single(invocation => invocation.Identifier == "createMarkdownViewerSession")
            .Arguments
            .OfType<DotNetObjectReference<MarkdownViewerInteropSession>>()
            .Single();
        var callbackTarget = callbackReference.Value;

        await callbackTarget.OnHashChangedAsync("before-disposal");
        callbackCount.Should().Be(1);
        var shutdown = fixture.ViewerSession.Plan("shutdown");
        shutdown.Complete();
        await fixture.Session.DisposeAsync();

        await callbackTarget.OnHashChangedAsync("after-disposal");

        callbackCount.Should().Be(1);
    }

    [Fact]
    public async Task OnHashChangedAsync_WhenDisposalHasStarted_ShouldIgnoreCallbackBeforeShutdownCompletes()
    {
        var callbackCount = 0;
        var fixture = await InteropFixture.CreateInitializedAsync(_ =>
        {
            callbackCount++;
            return Task.CompletedTask;
        });
        var callbackReference = fixture.Module.Invocations
            .Single(invocation => invocation.Identifier == "createMarkdownViewerSession")
            .Arguments
            .OfType<DotNetObjectReference<MarkdownViewerInteropSession>>()
            .Single();
        var callbackTarget = callbackReference.Value;
        var shutdown = fixture.ViewerSession.Plan("shutdown");

        var disposal = fixture.Session.DisposeAsync().AsTask();
        await shutdown.Started.WaitAsync(TestContext.Current.CancellationToken);

        await callbackTarget.OnHashChangedAsync("during-shutdown");

        callbackCount.Should().Be(0);
        fixture.ViewerSession.DisposeCount.Should().Be(0);
        fixture.Module.DisposeCount.Should().Be(0);
        shutdown.Complete();
        await disposal;
        fixture.ViewerSession.DisposeCount.Should().Be(1);
        fixture.Module.DisposeCount.Should().Be(1);
    }

    private static async Task AwaitExpectedCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed class InteropFixture
    {
        internal InteropFixture(Func<string?, Task>? hashChangedAsync = null)
        {
            EventLog = new JsInteropEventLog();
            Import = new ControlledJsOperation(EventLog, "runtime.import");
            Runtime = new ControlledJsRuntime(EventLog, Import, MARKDOWN_LAYOUT_MODULE_PATH);
            Module = new ControlledJsObjectReference(EventLog, "module");
            ViewerSession = new ControlledJsObjectReference(EventLog, "viewer");
            Session = new MarkdownViewerInteropSession(
                Runtime,
                hashChangedAsync ?? (_ => Task.CompletedTask));
        }

        internal JsInteropEventLog EventLog { get; }

        internal ControlledJsOperation Import { get; }

        internal ControlledJsRuntime Runtime { get; }

        internal ControlledJsObjectReference Module { get; }

        internal ControlledJsObjectReference ViewerSession { get; }

        internal MarkdownViewerInteropSession Session { get; }

        internal static async Task<InteropFixture> CreateInitializedAsync(
            Func<string?, Task>? hashChangedAsync = null)
        {
            var fixture = new InteropFixture(hashChangedAsync);
            fixture.Import.Complete(fixture.Module);
            var shouldShowSidebar = fixture.Module.Plan("shouldShowSidebarByDefault");
            shouldShowSidebar.Complete(true);
            var createSession = fixture.Module.Plan("createMarkdownViewerSession");
            createSession.Complete(fixture.ViewerSession);

            var shouldShowSidebarResult = await fixture.Session.InitializeAsync(default);
            shouldShowSidebarResult.Should().Be(true);
            return fixture;
        }
    }
}
