using Monica.Core.Results;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;
using Monica.JobScheduler.UI.UIJobScheduler.Support;
using MudBlazor;

namespace Monica.JobScheduler.UI.UIJobScheduler.Executions.State;

/// <summary>
/// Owns one execution-ledger page's filters, paging, durable query results, mutations, and async lifetime.
/// </summary>
internal sealed class JobExecutionsPageState : IAsyncDisposable
{
    private readonly JobSchedulerFacade _facade;
    private readonly IJobSchedulerUiAccess _access;
    private readonly TimeProvider _timeProvider;
    private readonly SchedulerTimePresentation _timePresentation;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private HashSet<JobExecutionState> _selectedStates = [];
    private bool _disposed;

    internal JobExecutionsPageState(
        JobSchedulerFacade facade,
        IJobSchedulerUiAccess access,
        int defaultPageSize,
        TimeProvider timeProvider,
        SchedulerTimePresentation timePresentation)
    {
        _facade = facade;
        _access = access;
        _timeProvider = timeProvider;
        _timePresentation = timePresentation;
        PageSize = Math.Clamp(defaultPageSize, 1, 200);
        PageSizeOptions = [.. new[] { 10, 20, 50, 100, PageSize }.Distinct().Order()];
    }

    /// <summary>
    /// Raised when consumers should render a new ledger snapshot.
    /// </summary>
    public event Func<Task>? StateChanged;

    /// <summary>
    /// Gets the current bounded execution page.
    /// </summary>
    public IReadOnlyList<JobExecutionInstance> Executions { get; private set; } = [];

    /// <summary>
    /// Gets whether authorization has been evaluated for the current page.
    /// </summary>
    public bool AccessChecked { get; private set; }

    /// <summary>
    /// Gets whether the current operator can inspect scheduler data.
    /// </summary>
    public bool IsAuthorized { get; private set; }

    /// <summary>
    /// Gets whether a ledger query is in progress.
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Gets whether an operator mutation is in progress.
    /// </summary>
    public bool IsMutating { get; private set; }

    /// <summary>
    /// Gets the latest query error, if any.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Gets or sets the case-insensitive instance or job-key search value.
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// Gets the selected execution states. An empty collection includes every state.
    /// </summary>
    public IReadOnlyCollection<JobExecutionState> SelectedStates => _selectedStates;

    /// <summary>
    /// Gets or sets the enqueue-time range preset.
    /// </summary>
    public ExecutionTimeRange TimeRange { get; set; }

    /// <summary>
    /// Gets or sets the optional custom scheduler-zone start date.
    /// </summary>
    public DateTime? CustomStartDate { get; set; }

    /// <summary>
    /// Gets or sets the optional custom scheduler-zone end date.
    /// </summary>
    public DateTime? CustomEndDate { get; set; }

    /// <summary>
    /// Gets whether the custom start date is later than the custom end date.
    /// </summary>
    public bool HasInvalidCustomRange =>
        TimeRange == ExecutionTimeRange.Custom
        && CustomStartDate.HasValue
        && CustomEndDate.HasValue
        && CustomStartDate.Value.Date > CustomEndDate.Value.Date;

    /// <summary>
    /// Gets the selected backend sort field.
    /// </summary>
    public JobExecutionSortField SortField { get; private set; } = JobExecutionSortField.CreatedAtUtc;

    /// <summary>
    /// Gets whether the ledger is ordered newest-first for the selected field.
    /// </summary>
    public bool SortDescending { get; private set; } = true;

    /// <summary>
    /// Gets the current one-based page number.
    /// </summary>
    public int PageNumber { get; private set; } = 1;

    /// <summary>
    /// Gets the available native table page-size choices, including the configured default.
    /// </summary>
    public int[] PageSizeOptions { get; }

    /// <summary>
    /// Gets the selected bounded page size.
    /// </summary>
    public int PageSize { get; private set; }

    /// <summary>
    /// Gets the total number of executions matching the current query.
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// Gets when the current ledger snapshot was observed.
    /// </summary>
    public DateTimeOffset? ObservedAtUtc { get; private set; }

    /// <summary>
    /// Evaluates access before the native table requests its first server page.
    /// </summary>
    public async Task InitializeAsync()
    {
        ThrowIfDisposed();
        var cancellationToken = _lifetimeCancellation.Token;
        try
        {
            IsLoading = true;
            await NotifyStateChangedAsync();
            IsAuthorized = await _access.IsAuthorizedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            AccessChecked = true;
            if (!IsAuthorized)
            {
                ClearResults();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Error = exception.Message;
            AccessChecked = true;
            IsAuthorized = false;
        }
        finally
        {
            if (!_disposed)
            {
                IsLoading = false;
                await NotifyStateChangedAsync();
            }
        }
    }

    /// <summary>
    /// Loads one server-backed table page using the table's native sort and paging state.
    /// </summary>
    public async Task<TableData<JobExecutionInstance>> LoadTableAsync(
        TableState tableState,
        CancellationToken requestCancellation)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(tableState);

        PageNumber = tableState.Page + 1;
        PageSize = Math.Clamp(tableState.PageSize, 1, 200);
        SortField = ResolveSortField(tableState.SortLabel);
        SortDescending = tableState.SortDirection != SortDirection.Ascending;

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetimeCancellation.Token,
            requestCancellation);
        await LoadPageAsync(linkedCancellation.Token);
        linkedCancellation.Token.ThrowIfCancellationRequested();
        return new TableData<JobExecutionInstance>
        {
            Items = Executions,
            TotalItems = TotalCount
        };
    }

    /// <summary>
    /// Applies optional route query values before the initial ledger query.
    /// </summary>
    public void ApplyInitialQuery(string? states, string? instanceId)
    {
        ThrowIfDisposed();
        _selectedStates = ParseStates(states);
        SearchText = string.IsNullOrWhiteSpace(instanceId) ? null : instanceId.Trim();
    }

    /// <summary>
    /// Replaces the multi-state selection without executing a query.
    /// </summary>
    public void SetSelectedStates(IEnumerable<JobExecutionState>? states)
    {
        ThrowIfDisposed();
        _selectedStates = states?.ToHashSet() ?? [];
    }

    /// <summary>
    /// Clears all filters while preserving the native table's current ordering.
    /// </summary>
    public void ResetFilters()
    {
        ThrowIfDisposed();
        SearchText = null;
        _selectedStates = [];
        TimeRange = ExecutionTimeRange.All;
        CustomStartDate = null;
        CustomEndDate = null;
    }

    /// <summary>
    /// Reauthorizes and durably requests cancellation before refreshing the ledger.
    /// </summary>
    public async Task<ExecutionCancellationUiResult> RequestCancellationAsync(string instanceId)
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

            IsAuthorized = await _access.IsAuthorizedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            AccessChecked = true;
            if (!IsAuthorized)
            {
                ClearResults();
                return new ExecutionCancellationUiResult(false, null, null);
            }

            var result = await _facade.CancelExecutionAsync(instanceId, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsFailed(out var error, out var cancellation))
            {
                return new ExecutionCancellationUiResult(true, null, error.Message);
            }

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

    private async Task LoadPageAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (HasInvalidCustomRange)
        {
            ClearResults();
            await NotifyStateChangedAsync();
            return;
        }

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
            await NotifyStateChangedAsync();

            IsAuthorized = await _access.IsAuthorizedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            AccessChecked = true;
            if (!IsAuthorized)
            {
                ClearResults();
                return;
            }

            var observedAtUtc = _timeProvider.GetUtcNow();
            var (createdAfterUtc, createdBeforeUtc) = ResolveTimeRange(observedAtUtc);
            var result = await _facade.QueryExecutionsAsync(new JobExecutionQuery
            {
                SchedulerScopeKey = string.Empty,
                SearchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                States = _selectedStates.Count == 0 ? null : _selectedStates.ToArray(),
                CreatedAfterUtc = createdAfterUtc,
                CreatedBeforeUtc = createdBeforeUtc,
                SortField = SortField,
                SortDescending = SortDescending,
                PageNumber = PageNumber,
                PageSize = PageSize
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsFailed(out var error, out var page))
            {
                Error = error.Message;
                return;
            }

            Executions = page.Items;
            TotalCount = page.TotalCount;
            ObservedAtUtc = observedAtUtc;
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

    private (DateTimeOffset? StartUtc, DateTimeOffset? EndUtc) ResolveTimeRange(DateTimeOffset observedAtUtc)
    {
        return TimeRange switch
        {
            ExecutionTimeRange.LastHour => (observedAtUtc.AddHours(-1), null),
            ExecutionTimeRange.Last24Hours => (observedAtUtc.AddDays(-1), null),
            ExecutionTimeRange.Last7Days => (observedAtUtc.AddDays(-7), null),
            ExecutionTimeRange.Last30Days => (observedAtUtc.AddDays(-30), null),
            ExecutionTimeRange.Custom => (
                ToUtcCalendarBoundary(CustomStartDate, endOfDay: false),
                ToUtcCalendarBoundary(CustomEndDate, endOfDay: true)),
            _ => (null, null)
        };
    }

    private static HashSet<JobExecutionState> ParseStates(string? values)
    {
        if (string.IsNullOrWhiteSpace(values))
        {
            return [];
        }

        HashSet<JobExecutionState> states = [];
        foreach (var value in values.Split(
                     ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<JobExecutionState>(value, ignoreCase: true, out var state))
            {
                states.Add(state);
            }
        }

        return states;
    }

    private static JobExecutionSortField ResolveSortField(string? sortLabel) =>
        Enum.TryParse<JobExecutionSortField>(sortLabel, ignoreCase: false, out var field)
            ? field
            : JobExecutionSortField.CreatedAtUtc;

    private DateTimeOffset? ToUtcCalendarBoundary(DateTime? value, bool endOfDay)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var schedulerWallTime = endOfDay
            ? value.Value.Date.AddDays(1).AddTicks(-1)
            : value.Value.Date;
        return _timePresentation.ConvertSchedulerWallTimeToUtc(schedulerWallTime);
    }

    private void ClearResults()
    {
        Executions = [];
        TotalCount = 0;
        PageNumber = 1;
        ObservedAtUtc = null;
        Error = null;
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
