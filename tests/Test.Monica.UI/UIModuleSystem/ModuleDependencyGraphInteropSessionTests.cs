using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Monica.UI.UIModuleSystem.Support;
using Xunit;

namespace Test.Monica.UI.UIModuleSystem;

public sealed class ModuleDependencyGraphInteropSessionTests
{
    [Fact]
    public async Task Two_graph_sessions_own_distinct_handles_and_cleanup_only_their_own_instance()
    {
        var firstHandle = new ControlledJsReference();
        var secondHandle = new ControlledJsReference();
        var firstModule = CreateModule(firstHandle);
        var secondModule = CreateModule(secondHandle);
        var modules = new Queue<IJSObjectReference>([firstModule, secondModule]);
        var runtime = new ControlledJsRuntime(() => ValueTask.FromResult(modules.Dequeue()));
        await using var first = new ModuleDependencyGraphInteropSession(runtime);
        await using var second = new ModuleDependencyGraphInteropSession(runtime);

        await first.RenderAsync(default(ElementReference), new { nodes = Array.Empty<object>() }, "first");
        await second.RenderAsync(default(ElementReference), new { nodes = Array.Empty<object>() }, "second");
        await first.DisposeAsync();

        firstHandle.InvocationIdentifiers.Should().ContainSingle().Which.Should().Be("dispose");
        firstHandle.DisposeCalls.Should().Be(1);
        secondHandle.InvocationIdentifiers.Should().BeEmpty();
        secondHandle.DisposeCalls.Should().Be(0);

        await second.DisposeAsync();
        secondHandle.InvocationIdentifiers.Should().ContainSingle().Which.Should().Be("dispose");
        secondHandle.DisposeCalls.Should().Be(1);
    }

    [Fact]
    public async Task Dispose_during_late_import_releases_the_imported_module_without_creating_a_graph()
    {
        var importCompletion = new TaskCompletionSource<IJSObjectReference>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var importStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var module = new ControlledJsReference();
        var runtime = new ControlledJsRuntime(() =>
        {
            importStarted.TrySetResult(true);
            return new ValueTask<IJSObjectReference>(importCompletion.Task);
        });
        var session = new ModuleDependencyGraphInteropSession(runtime);

        var render = session.RenderAsync(default, new { }, "late-import");
        await importStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        var dispose = session.DisposeAsync().AsTask();
        importCompletion.SetResult(module);
        await Task.WhenAll(render, dispose);

        module.InvocationIdentifiers.Should().BeEmpty();
        module.DisposeCalls.Should().Be(1);
    }

    [Fact]
    public async Task Dispose_during_late_handle_creation_releases_the_late_handle_and_module()
    {
        var createCompletion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var createStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = new ControlledJsReference();
        var module = new ControlledJsReference((identifier, _) =>
        {
            identifier.Should().Be("createGraph");
            createStarted.TrySetResult(true);
            return new ValueTask<object?>(createCompletion.Task);
        });
        var runtime = new ControlledJsRuntime(() => ValueTask.FromResult<IJSObjectReference>(module));
        var session = new ModuleDependencyGraphInteropSession(runtime);

        var render = session.RenderAsync(default, new { }, "late-handle");
        await createStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        var dispose = session.DisposeAsync().AsTask();
        createCompletion.SetResult(handle);
        await Task.WhenAll(render, dispose);

        handle.InvocationIdentifiers.Should().ContainSingle().Which.Should().Be("dispose");
        handle.DisposeCalls.Should().Be(1);
        module.DisposeCalls.Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_repeated_disposal_waits_for_one_safe_cleanup()
    {
        var cleanupStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = new ControlledJsReference(
            invoke: async (_, _) =>
            {
                cleanupStarted.TrySetResult(true);
                await cleanupRelease.Task;
                throw new JSException("context destroyed");
            },
            dispose: () => ValueTask.FromException(new JSException("context destroyed")));
        var module = CreateModule(
            handle,
            dispose: () => ValueTask.FromException(new JSException("context destroyed")));
        var runtime = new ControlledJsRuntime(() => ValueTask.FromResult<IJSObjectReference>(module));
        var session = new ModuleDependencyGraphInteropSession(runtime);
        await session.RenderAsync(default, new { }, "circuit-loss");

        var disposals = Enumerable.Range(0, 8)
            .Select(_ => session.DisposeAsync().AsTask())
            .ToArray();
        await cleanupStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        disposals.Should().OnlyContain(static disposal => !disposal.IsCompleted);
        cleanupRelease.SetResult(true);

        Func<Task> awaitAll = () => Task.WhenAll(disposals);
        await awaitAll.Should().NotThrowAsync();
        handle.InvocationIdentifiers.Should().ContainSingle().Which.Should().Be("dispose");
        handle.DisposeCalls.Should().Be(1);
        module.DisposeCalls.Should().Be(1);
    }

    [Fact]
    public async Task Circuit_disconnection_during_graph_and_module_cleanup_is_safe()
    {
        var handle = new ControlledJsReference(
            invoke: (_, _) => ValueTask.FromException<object?>(
                new JSDisconnectedException("circuit disconnected")),
            dispose: () => ValueTask.FromException(
                new JSDisconnectedException("circuit disconnected")));
        var module = CreateModule(
            handle,
            dispose: () => ValueTask.FromException(
                new JSDisconnectedException("circuit disconnected")));
        var runtime = new ControlledJsRuntime(() => ValueTask.FromResult<IJSObjectReference>(module));
        var session = new ModuleDependencyGraphInteropSession(runtime);
        await session.RenderAsync(default, new { }, "circuit-disconnected");

        Func<Task> dispose = async () => await session.DisposeAsync();

        await dispose.Should().NotThrowAsync();
        handle.InvocationIdentifiers.Should().ContainSingle().Which.Should().Be("dispose");
        handle.DisposeCalls.Should().Be(1);
        module.DisposeCalls.Should().Be(1);
    }

    [Fact]
    public async Task Circuit_disconnection_during_a_late_render_is_safe()
    {
        var runtime = new ControlledJsRuntime(() =>
            ValueTask.FromException<IJSObjectReference>(
                new JSDisconnectedException("circuit disconnected")));
        await using var session = new ModuleDependencyGraphInteropSession(runtime);

        Func<Task> render = async () =>
        {
            var outcome = await session.RenderAsync(default, new { }, "disconnected-render");
            outcome.Should().Be(ModuleDependencyGraphRenderOutcome.Cancelled);
        };

        await render.Should().NotThrowAsync();
        runtime.ImportCalls.Should().Be(1);
    }

    [Fact]
    public async Task Render_WhenGraphCreationFails_ShouldReturnFailureAndPermitDeterministicRetry()
    {
        var handle = new ControlledJsReference();
        var createAttempts = 0;
        var module = new ControlledJsReference((identifier, _) =>
        {
            identifier.Should().Be("createGraph");
            createAttempts++;
            return createAttempts == 1
                ? ValueTask.FromException<object?>(new JSException("initialization failed"))
                : ValueTask.FromResult<object?>(handle);
        });
        var runtime = new ControlledJsRuntime(() => ValueTask.FromResult<IJSObjectReference>(module));
        await using var session = new ModuleDependencyGraphInteropSession(runtime);

        var failed = await session.RenderAsync(default, new { }, "same-graph");
        var retried = await session.RenderAsync(default, new { }, "same-graph");

        failed.Should().Be(ModuleDependencyGraphRenderOutcome.Failed);
        retried.Should().Be(ModuleDependencyGraphRenderOutcome.Rendered);
        createAttempts.Should().Be(2);
        runtime.ImportCalls.Should().Be(1);
    }

    private static ControlledJsReference CreateModule(
        IJSObjectReference handle,
        Func<ValueTask>? dispose = null) => new(
        (identifier, _) =>
        {
            identifier.Should().Be("createGraph");
            return ValueTask.FromResult<object?>(handle);
        },
        dispose);
}

internal sealed class ControlledJsRuntime(
    Func<ValueTask<IJSObjectReference>> import) : IJSRuntime
{
    public int ImportCalls { get; private set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public async ValueTask<TValue> InvokeAsync<TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args)
    {
        identifier.Should().Be("import");
        ImportCalls++;
        return (TValue)(object)await import();
    }
}

internal sealed class ControlledJsReference(
    Func<string, object?[]?, ValueTask<object?>>? invoke = null,
    Func<ValueTask>? dispose = null) : IJSObjectReference
{
    public List<string> InvocationIdentifiers { get; } = [];
    public int DisposeCalls { get; private set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public async ValueTask<TValue> InvokeAsync<TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args)
    {
        InvocationIdentifiers.Add(identifier);
        var result = invoke is null ? null : await invoke(identifier, args);
        return result is null ? default! : (TValue)result;
    }

    public ValueTask DisposeAsync()
    {
        DisposeCalls++;
        return dispose?.Invoke() ?? ValueTask.CompletedTask;
    }
}
