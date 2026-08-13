using Microsoft.Extensions.Localization;
using Monica.Core.Results;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Models.Operations;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;

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
    internal int PageSize { get; }
    internal int PageCount { get; private set; } = 1;
    internal int TotalCount { get; private set; }
    internal bool HasSelection => _selectedJobKeys.Count > 0;
    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    internal Task InitializeAsync() => LoadAsync(clearSelection: true);

    internal async Task SearchAsync()
    {
        PageNumber = 1;
        await LoadAsync(clearSelection: true);
    }

    internal async Task ResetAsync()
    {
        SearchText = null;
        OwnerId = null;
        SelectedJobType = JobType.Recurring;
        SelectedDisabledState = null;
        PageNumber = 1;
        await LoadAsync(clearSelection: true);
    }

    internal async Task ChangePageAsync(int page)
    {
        PageNumber = Math.Max(1, page);
        await LoadAsync(clearSelection: true);
    }

    internal Task RefreshAsync() => LoadAsync(clearSelection: false);

    internal async Task SelectJobTypeAsync(JobType jobType)
    {
        if (SelectedJobType == jobType)
        {
            return;
        }

        SelectedJobType = jobType;
        PageNumber = 1;
        await LoadAsync(clearSelection: true);
    }

    internal async Task SelectDisabledStateAsync(bool? disabled)
    {
        if (SelectedDisabledState == disabled)
        {
            return;
        }

        SelectedDisabledState = disabled;
        PageNumber = 1;
        await LoadAsync(clearSelection: true);
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
        items[index] = items[index] with
        {
            Definition = updatedDefinition,
            RecurringScheduleStatus = isRecurring
                ? updatedDefinition.IsDisabled
                    ? JobRecurringScheduleStatus.Suspended
                    : JobRecurringScheduleStatus.AwaitingSynchronization
                : JobRecurringScheduleStatus.NotRecurring,
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

            var definition = summary.Definition;
            var result = await _facade.UpdatePolicyAsync(
                definition.OwnerId,
                definition.Declaration.JobKey,
                new JobPolicyChange
                {
                    DisabledOverride = disabled,
                    MaxRetainedHistoryRecords = definition.Policy.MaxRetainedHistoryRecords,
                    MaxRetentionDays = definition.Policy.MaxRetentionDays,
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
                    JobKey = definition.Declaration.JobKey,
                    ExpectedOwnerId = definition.OwnerId,
                    ExpectedJobRevisionId = definition.JobRevisionId
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

            var result = await _facade.UpdatePoliciesAsync(
                new JobPolicyBatchUpdateRequest
                {
                    Items = selected.Select(summary => new JobPolicyBatchUpdateItem
                    {
                        OwnerId = summary.Definition.OwnerId,
                        JobKey = summary.Definition.Declaration.JobKey,
                        DisabledOverride = disabled,
                        MaxRetainedHistoryRecords = summary.Definition.Policy.MaxRetainedHistoryRecords,
                        MaxRetentionDays = summary.Definition.Policy.MaxRetentionDays,
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

    private async Task LoadAsync(bool clearSelection)
    {
        var cancellationToken = _lifetimeCancellation.Token;
        await _loadGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            IsLoading = true;
            await NotifyChangedAsync();

            if (!await EnsureAuthorizedAsync(cancellationToken))
            {
                Summaries = [];
                TotalCount = 0;
                PageCount = 1;
                Error = null;
                return;
            }

            await LoadFacetsAsync(cancellationToken);

            var result = await _facade.QueryOperationalSummariesAsync(new JobCatalogQuery
            {
                SearchText = SearchText,
                OwnerId = OwnerId,
                JobType = SelectedJobType,
                IsDisabled = SelectedDisabledState,
                PageNumber = PageNumber,
                PageSize = PageSize
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsFailed(out var error, out var page))
            {
                Error = error.Message;
                return;
            }

            Summaries = page.Items;
            TotalCount = page.TotalCount;
            PageCount = Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
            Error = null;
            if (clearSelection)
            {
                _selectedJobKeys.Clear();
            }
            else
            {
                _selectedJobKeys.IntersectWith(Summaries.Select(static summary =>
                    summary.Definition.Declaration.JobKey));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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

        var activeDefinitions = overview.ActiveCatalog?.Definitions ?? [];
        OwnerFacets = activeDefinitions
            .GroupBy(static definition => definition.OwnerId, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => KeyValuePair.Create(group.Key, group.Count()))
            .ToArray();
        JobTypeCounts = activeDefinitions
            .GroupBy(static definition => definition.Declaration.JobType)
            .ToDictionary(static group => group.Key, static group => group.Count());
    }

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
