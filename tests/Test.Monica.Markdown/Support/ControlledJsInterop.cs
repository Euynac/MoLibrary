using Microsoft.JSInterop;

namespace Test.Monica.Markdown.Support;

internal sealed class JsInteropEventLog
{
    private readonly Lock _lock = new();
    private readonly List<string> _events = [];

    public IReadOnlyList<string> Events
    {
        get
        {
            lock (_lock)
            {
                return _events.ToArray();
            }
        }
    }

    public void Add(string value)
    {
        lock (_lock)
        {
            _events.Add(value);
        }
    }
}

internal sealed class ControlledJsOperation(
    JsInteropEventLog eventLog,
    string name,
    bool observeCancellation = false)
{
    private readonly TaskCompletionSource _started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<object?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;

    public void Complete(object? result = null) => _completion.TrySetResult(result);

    public void Fail(Exception exception) => _completion.TrySetException(exception);

    public async ValueTask<TValue> InvokeAsync<TValue>(CancellationToken cancellationToken)
    {
        eventLog.Add($"{name}:started");
        _started.TrySetResult();

        var result = observeCancellation
            ? await _completion.Task.WaitAsync(cancellationToken)
            : await _completion.Task;

        eventLog.Add($"{name}:completed");
        return result is null ? default! : (TValue)result;
    }
}

internal sealed class ControlledJsRuntime(
    JsInteropEventLog eventLog,
    ControlledJsOperation importOperation,
    string expectedModulePath) : IJSRuntime
{
    private int _invocationCount;

    public int InvocationCount => Volatile.Read(ref _invocationCount);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args)
    {
        if (!string.Equals(identifier, "import", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected runtime invocation '{identifier}'.");
        }

        if (args is not [string modulePath]
            || !string.Equals(modulePath, expectedModulePath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The markdown viewer imported an unexpected JavaScript module.");
        }

        Interlocked.Increment(ref _invocationCount);
        eventLog.Add("runtime.import:accepted");
        return importOperation.InvokeAsync<TValue>(cancellationToken);
    }
}

internal sealed class ControlledJsObjectReference(
    JsInteropEventLog eventLog,
    string name) : IJSObjectReference
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Queue<ControlledJsOperation>> _operations =
        new(StringComparer.Ordinal);
    private readonly List<(string Identifier, object?[] Arguments)> _invocations = [];
    private int _disposeCount;
    private int _isDisposed;

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public IReadOnlyList<(string Identifier, object?[] Arguments)> Invocations
    {
        get
        {
            lock (_lock)
            {
                return _invocations
                    .Select(static invocation =>
                        (invocation.Identifier, invocation.Arguments.ToArray()))
                    .ToArray();
            }
        }
    }

    public ControlledJsOperation Plan(
        string identifier,
        bool observeCancellation = false)
    {
        var operation = new ControlledJsOperation(
            eventLog,
            $"{name}.{identifier}",
            observeCancellation);

        lock (_lock)
        {
            if (!_operations.TryGetValue(identifier, out var operations))
            {
                operations = new Queue<ControlledJsOperation>();
                _operations.Add(identifier, operations);
            }

            operations.Enqueue(operation);
        }

        return operation;
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _isDisposed) != 0,
            this);

        ControlledJsOperation operation;

        lock (_lock)
        {
            if (!_operations.TryGetValue(identifier, out var operations)
                || operations.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Unexpected invocation '{name}.{identifier}'.");
            }

            operation = operations.Dequeue();
            _invocations.Add((identifier, args?.ToArray() ?? []));
        }

        return operation.InvokeAsync<TValue>(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        Volatile.Write(ref _isDisposed, 1);
        Interlocked.Increment(ref _disposeCount);
        eventLog.Add($"{name}.dispose");
        return ValueTask.CompletedTask;
    }
}
