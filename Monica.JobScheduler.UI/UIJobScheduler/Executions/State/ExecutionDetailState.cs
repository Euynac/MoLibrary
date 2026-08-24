using Monica.Core.Results;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;

namespace Monica.JobScheduler.UI.UIJobScheduler.Executions.State;

/// <summary>
/// Owns one execution-detail dialog's evidence snapshot, mutation boundary, and async lifetime.
/// </summary>
internal sealed class ExecutionDetailState(
    JobSchedulerFacade facade,
    IJobSchedulerUiAccess access,
    TimeProvider timeProvider) : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Raised when consumers should render a new detail snapshot.
    /// </summary>
    public event Func<Task>? StateChanged;

    /// <summary>
    /// Gets the requested execution identifier.
    /// </summary>
    public string InstanceId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the complete durable execution evidence snapshot.
    /// </summary>
    public JobExecutionInstance? Execution { get; private set; }

    /// <summary>
    /// Gets whether authorization has been evaluated.
    /// </summary>
    public bool AccessChecked { get; private set; }

    /// <summary>
    /// Gets whether the current operator can inspect the execution.
    /// </summary>
    public bool IsAuthorized { get; private set; }

    /// <summary>
    /// Gets whether the execution evidence is loading.
    /// </summary>
    public bool IsLoading { get; private set; } = true;

    /// <summary>
    /// Gets whether a cancellation mutation is in progress.
    /// </summary>
    public bool IsMutating { get; private set; }

    /// <summary>
    /// Gets whether the execution did not exist at the time of the latest read.
    /// </summary>
    public bool IsNotFound { get; private set; }

    /// <summary>
    /// Gets the latest detail-load error, if any.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Gets when the current detail snapshot was observed.
    /// </summary>
    public DateTimeOffset? ObservedAtUtc { get; private set; }

    /// <summary>
    /// Gets the component-owned lifetime token for cancellable UI interop.
    /// </summary>
    public CancellationToken LifetimeToken => _lifetimeCancellation.Token;

    /// <summary>
    /// Loads a complete evidence snapshot for the supplied execution.
    /// </summary>
    public async Task InitializeAsync(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ThrowIfDisposed();
        InstanceId = instanceId;
        await LoadAsync();
    }

    /// <summary>
    /// Reloads the current complete evidence snapshot.
    /// </summary>
    public Task RefreshAsync()
    {
        ThrowIfDisposed();
        return LoadAsync();
    }

    /// <summary>
    /// Reauthorizes and durably requests cancellation before refreshing the evidence snapshot.
    /// </summary>
    public async Task<ExecutionCancellationUiResult> RequestCancellationAsync()
    {
        ThrowIfDisposed();
        var cancellationToken = _lifetimeCancellation.Token;
        try
        {
            await _mutationGate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionCancellationUiResult(IsAuthorized, null, null);
        }

        try
        {
            ThrowIfDisposed();
            IsMutating = true;
            await NotifyStateChangedAsync();

            IsAuthorized = await access.IsAuthorizedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            AccessChecked = true;
            if (!IsAuthorized)
            {
                Execution = null;
                Error = null;
                return new ExecutionCancellationUiResult(false, null, null);
            }

            var result = await facade.CancelExecutionAsync(InstanceId, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsFailed(out var error, out var cancellation))
            {
                return new ExecutionCancellationUiResult(true, null, error.Message);
            }

            await LoadAsync();
            return new ExecutionCancellationUiResult(true, cancellation.Status, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionCancellationUiResult(IsAuthorized, null, null);
        }
        catch (Exception exception)
        {
            return new ExecutionCancellationUiResult(true, null, exception.Message);
        }
        finally
        {
            if (!_disposed)
            {
                IsMutating = false;
                await NotifyStateChangedAsync();
            }

            _mutationGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StateChanged = null;
        await _lifetimeCancellation.CancelAsync();
        await WaitForGateAsync(_loadGate);
        await WaitForGateAsync(_mutationGate);
        _loadGate.Dispose();
        _mutationGate.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private async Task LoadAsync()
    {
        ThrowIfDisposed();
        var cancellationToken = _lifetimeCancellation.Token;
        try
        {
            await _loadGate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            ThrowIfDisposed();
            IsLoading = true;
            Error = null;
            IsNotFound = false;
            await NotifyStateChangedAsync();

            IsAuthorized = await access.IsAuthorizedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            AccessChecked = true;
            if (!IsAuthorized)
            {
                Execution = null;
                return;
            }

            var result = await facade.GetExecutionAsync(InstanceId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsFailed(out var error, out var execution))
            {
                Error = error.Message;
                return;
            }

            Execution = execution;
            IsNotFound = execution is null;
            ObservedAtUtc = timeProvider.GetUtcNow();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Error = exception.Message;
        }
        finally
        {
            if (!_disposed)
            {
                IsLoading = false;
                await NotifyStateChangedAsync();
            }

            _loadGate.Release();
        }
    }

    private async Task NotifyStateChangedAsync()
    {
        var handlers = StateChanged?.GetInvocationList().Cast<Func<Task>>().ToArray() ?? [];
        foreach (var handler in handlers)
        {
            await handler();
        }
    }

    private static async Task WaitForGateAsync(SemaphoreSlim gate)
    {
        await gate.WaitAsync();
        gate.Release();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
