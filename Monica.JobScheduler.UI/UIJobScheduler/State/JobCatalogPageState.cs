using Microsoft.Extensions.Localization;
using Monica.Core.Results;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Definitions;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Models.Operations;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;
using MudBlazor;

namespace Monica.JobScheduler.UI.UIJobScheduler.State;

internal sealed class JobCatalogPageStateFactory(
    JobSchedulerFacade facade,
    IJobSchedulerUiAccess access,
    IStringLocalizer<JobSchedulerResource> localizer)
{
    internal JobCatalogPageState Create(int pageSize) => new(facade, access, localizer, pageSize);
}

/// <summary>
/// Owns one catalog page's bounded operational query, selection, mutations, and asynchronous lifetime.
/// </summary>
internal sealed class JobCatalogPageState : IAsyncDisposable
{
    private readonly JobSchedulerFacade _facade;
    private readonly IJobSchedulerUiAccess _access;
    private readonly IStringLocalizer<JobSchedulerResource> _localizer;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private readonly HashSet<string> _selectedJobKeys = new(StringComparer.Ordinal);
    private int _disposed;

    internal JobCatalogPageState(
        JobSchedulerFacade facade,
        IJobSchedulerUiAccess access,
        IStringLocalizer<JobSchedulerResource> localizer,
        int pageSize)
    {
        _facade = facade;
        _access = access;
        _localizer = localizer;
        PageSize = Math.Max(1, pageSize);
        PageSizeOptions = [.. new[] { 10, 20, 50, 100, PageSize }.Distinct().Order()];
    }

    internal event Func<Task>? Changed;

    internal IReadOnlyList<JobOperationalSummary> Summaries { get; private set; } = [];
    internal IReadOnlyList<KeyValuePair<string, int>> OwnerFacets { get; private set; } = [];
    internal IReadOnlyDictionary<JobType, int> JobTypeCounts { get; private set; } =
        new Dictionary<JobType, int>();
    internal IReadOnlySet<string> SelectedJobKeys => _selectedJobKeys;
    internal IReadOnlyList<JobOperationalSummary> SelectedRecurringSummaries => Summaries
        .Where(summary => summary.Definition.Declaration.JobType == JobType.Recurring
                          && _selectedJobKeys.Contains(summary.Definition.Declaration.JobKey))
        .ToArray();

    internal string? SearchText { get; set; }
    internal string? OwnerId { get; set; }
    internal JobType? SelectedJobType { get; set; } = JobType.Recurring;
    internal bool? SelectedDisabledState { get; set; }
    internal bool AccessChecked { get; private set; }
    internal bool IsAuthorized { get; private set; }
    internal bool IsLoading { get; private set; }
    internal bool IsMutating { get; private set; }
    internal string? Error { get; private set; }
    internal int PageNumber { get; private set; } = 1;
    internal int PageSize { get; private set; }
    internal int[] PageSizeOptions { get; }
    internal int TotalCount { get; private set; }
    internal JobDefinitionSortField SortField { get; private set; } = JobDefinitionSortField.JobName;
    internal bool SortDescending { get; private set; }
    internal bool HasSelection => _selectedJobKeys.Count > 0;
    internal bool HasDebugOnlySuppressedSelection =>
        SelectedRecurringSummaries.Any(JobSchedulerUiPresentation.IsDebugOnlySuppressed);
    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    internal async Task InitializeAsync()
    {
        var cancellationToken = _lifetimeCancellation.Token;
        try
        {
            IsLoading = true;
            await NotifyChangedAsync();
            if (!await EnsureAuthorizedAsync(cancellationToken))
            {
                ClearResults();
                return;
            }

            await LoadFacetsAsync(cancellationToken);
            Error = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!AccessChecked)
            {
                ClearResults();
                AccessChecked = true;
                IsAuthorized = false;
            }

            Error = exception.Message;
        }
        finally
        {
            if (!IsDisposed)
            {
                IsLoading = false;
                await NotifyChangedAsync();
            }
        }
    }

    internal void Search()
    {
        _selectedJobKeys.Clear();
    }

    internal void Reset()
    {
        SearchText = null;
        OwnerId = null;
        SelectedJobType = JobType.Recurring;
        SelectedDisabledState = null;
        _selectedJobKeys.Clear();
    }

    internal void SelectJobType(JobType jobType)
    {
        if (SelectedJobType == jobType)
        {
            return;
        }

        SelectedJobType = jobType;
        _selectedJobKeys.Clear();
    }

    internal void SelectDisabledState(bool? disabled)
    {
        if (SelectedDisabledState == disabled)
        {
            return;
        }

        SelectedDisabledState = disabled;
        _selectedJobKeys.Clear();
    }

    internal async Task<TableData<JobOperationalSummary>> LoadTableAsync(
        TableState tableState,
        CancellationToken requestCancellation)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(tableState);

        PageNumber = tableState.Page + 1;
        PageSize = Math.Clamp(tableState.PageSize, 1, 200);
        SortField = ResolveSortField(tableState.SortLabel);
        SortDescending = tableState.SortDirection == SortDirection.Descending;

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetimeCancellation.Token,
            requestCancellation);
        await LoadAsync(linkedCancellation.Token);
        linkedCancellation.Token.ThrowIfCancellationRequested();
        return new TableData<JobOperationalSummary>
        {
            Items = Summaries,
            TotalItems = TotalCount
        };
    }

    internal void ToggleSelection(JobOperationalSummary summary)
    {
        if (summary.Definition.Declaration.JobType != JobType.Recurring)
        {
            return;
        }

        var jobKey = summary.Definition.Declaration.JobKey;
        if (!_selectedJobKeys.Add(jobKey))
        {
            _selectedJobKeys.Remove(jobKey);
        }
    }

    internal void SelectAllRecurring(bool selected)
    {
        _selectedJobKeys.Clear();
        if (selected)
        {
            foreach (var summary in Summaries.Where(static summary =>
                         summary.Definition.Declaration.JobType == JobType.Recurring))
            {
                _selectedJobKeys.Add(summary.Definition.Declaration.JobKey);
            }
        }
    }

    internal void ApplyPolicy(string jobKey, JobPolicy policy)
    {
        var items = Summaries.ToArray();
        var index = Array.FindIndex(items, summary =>
            string.Equals(summary.Definition.Declaration.JobKey, jobKey, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        var updatedDefinition = items[index].Definition with { Policy = policy };
        var isRecurring = updatedDefinition.Declaration.JobType == JobType.Recurring;
        var suspensionReasons = isRecurring
            ? JobSchedulerUiPresentation.SetOperatorPolicySuspension(
                items[index].SuspensionReasons,
                updatedDefinition.IsDisabled)
            : JobRecurringScheduleSuspensionReason.None;
        items[index] = items[index] with
        {
            Definition = updatedDefinition,
            RecurringScheduleStatus = isRecurring
                ? suspensionReasons != JobRecurringScheduleSuspensionReason.None
                    ? JobRecurringScheduleStatus.Suspended
                    : JobRecurringScheduleStatus.AwaitingSynchronization
                : JobRecurringScheduleStatus.NotRecurring,
            SuspensionReasons = suspensionReasons,
            NextOccurrenceUtc = isRecurring ? null : items[index].NextOccurrenceUtc
        };
        Summaries = items;
    }

    internal async Task<Res<JobPolicy>> SetDisabledAsync(JobOperationalSummary summary, bool disabled)
    {
        var cancellationToken = _lifetimeCancellation.Token;
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            IsMutating = true;
            await NotifyChangedAsync();
            if (!await EnsureAuthorizedAsync(cancellationToken))
            {
                return Res.Fail(_localizer["Access:DeniedDescription"]);
            }

            if (disabled && JobSchedulerUiPresentation.IsDebugOnlySuppressed(summary))
            {
                return Res.Fail(_localizer["Catalog:Messages:PauseUnavailableDebug"]);
            }

            var definition = summary.Definition;
            var result = await _facade.UpdatePolicyAsync(
                definition.OwnerKey,
                definition.Declaration.JobKey,
                new JobPolicyChange
                {
                    Overrides = definition.Policy.Overrides with { DisabledOverride = disabled },
                    ExpectedConcurrencyStamp = definition.Policy.ConcurrencyStamp
                },
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!result.IsFailed(out _, out var policy))
            {
                ApplyPolicy(definition.Declaration.JobKey, policy);
            }

            return result;
        }
        finally
        {
            if (!IsDisposed)
            {
                IsMutating = false;
                await NotifyChangedAsync();
            }

            _mutationGate.Release();
        }
    }

    internal async Task<Res<JobExecutionInstance>> RunRecurringNowAsync(JobOperationalSummary summary)
    {
        var cancellationToken = _lifetimeCancellation.Token;
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            IsMutating = true;
            await NotifyChangedAsync();
            if (!await EnsureAuthorizedAsync(cancellationToken))
            {
                return Res.Fail(_localizer["Access:DeniedDescription"]);
            }

            var definition = summary.Definition;
            return await _facade.RunRecurringNowAsync(
                new JobRecurringRunNowRequest
                {
                    OwnerKey = definition.OwnerKey,
                    JobKey = definition.Declaration.JobKey
                },
                cancellationToken);
        }
        finally
        {
            if (!IsDisposed)
            {
                IsMutating = false;
                await NotifyChangedAsync();
            }

            _mutationGate.Release();
        }
    }

    internal async Task<Res<JobPolicyBatchUpdateResult>> SetSelectedDisabledAsync(bool disabled)
    {
        var cancellationToken = _lifetimeCancellation.Token;
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            IsMutating = true;
            await NotifyChangedAsync();
            if (!await EnsureAuthorizedAsync(cancellationToken))
            {
                return Res.Fail(_localizer["Access:DeniedDescription"]);
            }

            var selected = SelectedRecurringSummaries;
            if (selected.Count == 0)
            {
                return _localizer["Catalog:Batch:Empty"].Value;
            }

            if (disabled && selected.Any(JobSchedulerUiPresentation.IsDebugOnlySuppressed))
            {
                return _localizer["Catalog:Batch:PauseUnavailableDebug"].Value;
            }

            var result = await _facade.UpdatePoliciesAsync(
                new JobPolicyBatchUpdateRequest
                {
                    Items = selected.Select(summary => new JobPolicyBatchUpdateItem
                    {
                        OwnerKey = summary.Definition.OwnerKey,
                        JobKey = summary.Definition.Declaration.JobKey,
                        Overrides = summary.Definition.Policy.Overrides with { DisabledOverride = disabled },
                        ExpectedConcurrencyStamp = summary.Definition.Policy.ConcurrencyStamp
                    }).ToArray()
                },
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!result.IsFailed(out _, out var batch))
            {
                foreach (var item in batch.Items.Where(static item => item.IsSucceeded))
                {
                    ApplyPolicy(item.JobKey, item.Policy!);
                    _selectedJobKeys.Remove(item.JobKey);
                }
            }

            return result;
        }
        finally
        {
            if (!IsDisposed)
            {
                IsMutating = false;
                await NotifyChangedAsync();
            }

            _mutationGate.Release();
        }
    }

    internal async Task<bool> ReauthorizeAsync()
    {
        var cancellationToken = _lifetimeCancellation.Token;
        return await EnsureAuthorizedAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Changed = null;
        await _lifetimeCancellation.CancelAsync();
        await _loadGate.WaitAsync();
        _loadGate.Release();
        await _mutationGate.WaitAsync();
        _mutationGate.Release();
        _loadGate.Dispose();
        _mutationGate.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await _loadGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            IsLoading = true;
            await NotifyChangedAsync();

            if (!await EnsureAuthorizedAsync(cancellationToken))
            {
                ClearResults();
                return;
            }

            var result = await _facade.QueryOperationalSummariesAsync(new JobDefinitionQuery
            {
                SearchText = SearchText,
                OwnerKey = OwnerId,
                // The operator catalog defaults to definitions their owners currently publish; absent definitions
                // stay visible in the overview attention panel and through explicit queries.
                IsPresent = true,
                JobType = SelectedJobType,
                IsDisabled = SelectedDisabledState,
                SortField = SortField,
                SortDescending = SortDescending,
                PageNumber = PageNumber,
                PageSize = PageSize
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsFailed(out var error, out var page))
            {
                Summaries = [];
                TotalCount = 0;
                _selectedJobKeys.Clear();
                Error = error.Message;
                return;
            }

            Summaries = page.Items;
            TotalCount = page.TotalCount;
            Error = null;
            RetainVisibleSelection();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Summaries = [];
            TotalCount = 0;
            _selectedJobKeys.Clear();
            Error = exception.Message;
        }
        finally
        {
            if (!IsDisposed)
            {
                IsLoading = false;
                await NotifyChangedAsync();
            }

            _loadGate.Release();
        }
    }

    private async Task LoadFacetsAsync(CancellationToken cancellationToken)
    {
        var result = await _facade.GetOverviewAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (result.IsFailed(out _, out var overview))
        {
            return;
        }

        var presentDefinitions = overview.Definitions.Where(static definition => definition.IsPresent).ToArray();
        OwnerFacets = presentDefinitions
            .GroupBy(static definition => definition.OwnerKey, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => KeyValuePair.Create(group.Key, group.Count()))
            .ToArray();
        JobTypeCounts = presentDefinitions
            .GroupBy(static definition => definition.Declaration.JobType)
            .ToDictionary(static group => group.Key, static group => group.Count());
    }

    private void ClearResults()
    {
        Summaries = [];
        TotalCount = 0;
        PageNumber = 1;
        Error = null;
        _selectedJobKeys.Clear();
    }

    private void RetainVisibleSelection()
    {
        var visibleRecurringJobKeys = Summaries
            .Where(static summary => summary.Definition.Declaration.JobType == JobType.Recurring)
            .Select(static summary => summary.Definition.Declaration.JobKey)
            .ToHashSet(StringComparer.Ordinal);
        _selectedJobKeys.IntersectWith(visibleRecurringJobKeys);
    }

    private static JobDefinitionSortField ResolveSortField(string? sortLabel) =>
        Enum.TryParse<JobDefinitionSortField>(sortLabel, ignoreCase: false, out var field)
            ? field
            : JobDefinitionSortField.JobName;

    private async Task<bool> EnsureAuthorizedAsync(CancellationToken cancellationToken)
    {
        IsAuthorized = await _access.IsAuthorizedAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        AccessChecked = true;
        return IsAuthorized;
    }

    private async Task NotifyChangedAsync()
    {
        var handlers = Changed?.GetInvocationList().Cast<Func<Task>>().ToArray() ?? [];
        foreach (var handler in handlers)
        {
            await handler();
        }
    }
}
