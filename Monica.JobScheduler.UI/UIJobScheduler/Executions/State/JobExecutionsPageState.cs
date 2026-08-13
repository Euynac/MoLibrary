using Monica.Core.Results;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;

namespace Monica.JobScheduler.UI.UIJobScheduler.Executions.State;

/// <summary>
/// Owns one execution-ledger page's filters, paging, durable query results, mutations, and async lifetime.
/// </summary>
internal sealed class JobExecutionsPageState : IAsyncDisposable
{
    private readonly JobSchedulerFacade _facade;
    private readonly IJobSchedulerUiAccess _access;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private HashSet<JobExecutionState> _selectedStates = [];
    private bool _disposed;

    internal JobExecutionsPageState(
        JobSchedulerFacade facade,
        IJobSchedulerUiAccess access,
        int defaultPageSize,
        TimeProvider timeProvider)
    {
        _facade = facade;
        _access = access;
        _timeProvider = timeProvider;
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
    /// Gets or sets the optional custom local start date.
    /// </summary>
    public DateTime? CustomStartDate { get; set; }

    /// <summary>
    /// Gets or sets the optional custom local end date.
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
    /// Gets the available page-size choices, including the configured default.
    /// </summary>
    public IReadOnlyList<int> PageSizeOptions { get; }

    /// <summary>
    /// Gets the selected bounded page size.
    /// </summary>
    public int PageSize { get; private set; }

    /// <summary>
    /// Gets the number of available result pages.
    /// </summary>
    public int PageCount { get; private set; } = 1;

    /// <summary>
    /// Gets the total number of executions matching the current query.
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// Gets when the current ledger snapshot was observed.
    /// </summary>
    public DateTimeOffset? ObservedAtUtc { get; private set; }

    /// <summary>
    /// Loads the first ledger snapshot.
    /// </summary>
    public Task InitializeAsync() => LoadPageAsync();

    /// <summary>
    /// Re-runs the current bounded query without changing its filters or page.
    /// </summary>
    public Task RefreshAsync() => LoadPageAsync();

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
    /// Applies the current filter inputs from the first page.
    /// </summary>
    public async Task ApplyFiltersAsync()
    {
        ThrowIfDisposed();
        PageNumber = 1;
        await LoadPageAsync();
    }

    /// <summary>
    /// Clears all filters and restores chronological newest-first ordering.
    /// </summary>
    public async Task ResetFiltersAsync()
    {
        ThrowIfDisposed();
        SearchText = null;
        _selectedStates = [];
        TimeRange = ExecutionTimeRange.All;
        CustomStartDate = null;
        CustomEndDate = null;
        SortField = JobExecutionSortField.CreatedAtUtc;
        SortDescending = true;
        PageNumber = 1;
        await LoadPageAsync();
    }

    /// <summary>
    /// Selects a backend sort field and reloads from the first page.
    /// </summary>
    public async Task SetSortFieldAsync(JobExecutionSortField sortField)
    {
        ThrowIfDisposed();
        SortField = sortField;
        PageNumber = 1;
        await LoadPageAsync();
    }

    /// <summary>
    /// Reverses the selected backend ordering and reloads from the first page.
    /// </summary>
    public async Task ToggleSortDirectionAsync()
    {
        ThrowIfDisposed();
        SortDescending = !SortDescending;
        PageNumber = 1;
        await LoadPageAsync();
    }

    /// <summary>
    /// Changes the bounded page size and reloads from the first page.
    /// </summary>
    public async Task SetPageSizeAsync(int pageSize)
    {
        ThrowIfDisposed();
        PageSize = Math.Clamp(pageSize, 1, 200);
        PageNumber = 1;
        await LoadPageAsync();
    }

    /// <summary>
    /// Loads the requested one-based result page.
    /// </summary>
    public async Task SetPageAsync(int pageNumber)
    {
        ThrowIfDisposed();
        PageNumber = Math.Clamp(pageNumber, 1, PageCount);
        await LoadPageAsync();
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

            await LoadPageAsync();
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

    private async Task LoadPageAsync()
    {
        ThrowIfDisposed();
        if (HasInvalidCustomRange)
        {
            await NotifyStateChangedAsync();
            return;
        }

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
            PageCount = Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
            PageNumber = Math.Min(PageNumber, PageCount);
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

    private static DateTimeOffset? ToUtcCalendarBoundary(DateTime? value, bool endOfDay)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var localValue = endOfDay
            ? value.Value.Date.AddDays(1).AddTicks(-1)
            : value.Value.Date;
        return new DateTimeOffset(DateTime.SpecifyKind(localValue, DateTimeKind.Local)).ToUniversalTime();
    }

    private void ClearResults()
    {
        Executions = [];
        TotalCount = 0;
        PageCount = 1;
        PageNumber = 1;
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
