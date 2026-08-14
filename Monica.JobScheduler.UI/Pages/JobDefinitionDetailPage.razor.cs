using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Monica.Core.Results;
using Monica.JobScheduler.UI.Components;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.UIJobScheduler.Components;
using Monica.JobScheduler.UI.UIJobScheduler.Executions.Support;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;
using Monica.JobScheduler.UI.UIJobScheduler.State;
using Monica.JobScheduler.UI.UIJobScheduler.Support;
using MudBlazor;

namespace Monica.JobScheduler.UI.Pages;

/// <summary>
/// Composes one active job's immutable contract and bounded operational evidence.
/// </summary>
public partial class JobDefinitionDetailPage : IAsyncDisposable
{
    /// <summary>
    /// Gets the route pattern for one active logical job.
    /// </summary>
    public const string PAGE_URL = "/job-scheduler/catalog/{JobKey}";

    /// <summary>
    /// Gets the route-provided logical job key.
    /// </summary>
    [Parameter]
    public string JobKey { get; set; } = string.Empty;

    [Inject]
    private JobDefinitionDetailPageStateFactory PageStateFactory { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    [Inject]
    private IStringLocalizer<JobSchedulerResource> L { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private SchedulerTimePresentation TimePresentation { get; set; } = null!;

    [Inject]
    private TimeProvider TimeProvider { get; set; } = null!;

    private JobDefinitionDetailPageState? PageState { get; set; }
    private Func<Task>? _stateChangedHandler;
    private int _stateVersion;
    private bool _disposed;

    private string PageTitleText => PageState?.Summary?.Definition.Declaration.JobName
                                    ?? L["JobDetail:PageTitle"];

    protected override async Task OnParametersSetAsync()
    {
        var jobKey = JobKey;
        if (PageState is not null
            && string.Equals(PageState.JobKey, jobKey, StringComparison.Ordinal))
        {
            return;
        }

        await ReplaceStateAsync(jobKey);
    }

    private async Task ReplaceStateAsync(string jobKey)
    {
        var version = Interlocked.Increment(ref _stateVersion);
        var previousState = PageState;
        var previousHandler = _stateChangedHandler;
        PageState = null;
        _stateChangedHandler = null;
        if (previousState is not null)
        {
            if (previousHandler is not null)
            {
                previousState.Changed -= previousHandler;
            }

            await previousState.DisposeAsync();
        }

        if (!IsCurrentStateVersion(version))
        {
            return;
        }

        var state = PageStateFactory.Create(jobKey);
        Func<Task> stateChangedHandler = () => HandleStateChangedAsync(state, version);
        state.Changed += stateChangedHandler;
        if (!IsCurrentStateVersion(version))
        {
            state.Changed -= stateChangedHandler;
            await state.DisposeAsync();
            return;
        }

        PageState = state;
        _stateChangedHandler = stateChangedHandler;
        await state.InitializeAsync();
        if (!IsCurrentStateVersion(version) || !ReferenceEquals(PageState, state))
        {
            state.Changed -= stateChangedHandler;
            if (ReferenceEquals(PageState, state))
            {
                PageState = null;
                _stateChangedHandler = null;
            }

            await state.DisposeAsync();
        }
    }

    private async Task OpenScheduleInspectorAsync()
    {
        var state = PageState;
        if (state?.Summary is not { } summary)
        {
            return;
        }

        var parameters = new DialogParameters<CronScheduleInspectorDialog>
        {
            { dialog => dialog.Definition, summary.Definition },
            { dialog => dialog.OperationalSummary, summary },
            { dialog => dialog.ObservedAtUtc, state.ObservedAtUtc }
        };
        await DialogService.ShowAsync<CronScheduleInspectorDialog>(
            L["CronInspector:Title"],
            parameters,
            new DialogOptions
            {
                CloseButton = true,
                CloseOnEscapeKey = true,
                FullWidth = true,
                MaxWidth = MaxWidth.Large
            });
    }

    private async Task OpenExecutionDetailsAsync(string instanceId)
    {
        var state = PageState;
        if (_disposed || state is null || string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        await ExecutionDetailDialogLauncher.ShowAsync(DialogService, L, instanceId);
        if (!_disposed && ReferenceEquals(PageState, state))
        {
            await state.RefreshAsync();
        }
    }

    private async Task OpenPolicyAsync()
    {
        var state = PageState;
        if (_disposed || state?.Summary is not { } summary)
        {
            return;
        }

        var parameters = new DialogParameters<JobPolicyDialog>
        {
            { dialog => dialog.Definition, summary.Definition }
        };
        var dialog = await DialogService.ShowAsync<JobPolicyDialog>(
            L["Policy:Title"],
            parameters,
            SmallDialogOptions);
        var result = await dialog.Result;
        if (_disposed || PageState != state)
        {
            return;
        }

        if (result is { Canceled: false, Data: not null })
        {
            Snackbar.Add(L["Policy:Saved"], Severity.Success);
            await state.RefreshAsync();
        }
    }

    private async Task ToggleDisabledAsync()
    {
        var state = PageState;
        if (_disposed || state?.Summary is not { } summary)
        {
            return;
        }

        try
        {
            var operatorPaused = JobSchedulerUiPresentation.IsOperatorPaused(summary);
            var debugSuppressed = JobSchedulerUiPresentation.IsDebugSuppressed(summary);
            if (!operatorPaused && debugSuppressed)
            {
                Snackbar.Add(L["Catalog:Messages:PauseUnavailableDebug"], Severity.Info);
                return;
            }

            var disabled = !operatorPaused;
            var result = await state.SetDisabledAsync(disabled);
            if (_disposed || PageState != state)
            {
                return;
            }

            if (result.IsFailed(out var error))
            {
                Snackbar.Add(error.Message ?? L["Catalog:Messages:UpdateFailed"], Severity.Error);
                return;
            }

            var message = !disabled && debugSuppressed
                ? L["Catalog:Messages:ResumedDebugRemains"]
                : disabled
                    ? L["Catalog:Messages:Paused"]
                    : L["Catalog:Messages:Resumed"];
            Snackbar.Add(message, !disabled && debugSuppressed ? Severity.Info : Severity.Success);
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
    }

    private async Task RunRecurringNowAsync()
    {
        var state = PageState;
        if (_disposed || state?.Summary is not { } summary)
        {
            return;
        }

        try
        {
            var result = await state.RunRecurringNowAsync();
            if (_disposed || PageState != state)
            {
                return;
            }

            if (result.IsFailed(out var error, out var execution))
            {
                Snackbar.Add(error.Message ?? L["Catalog:Messages:RunFailed"], Severity.Error);
                return;
            }

            Snackbar.Add(L["Catalog:Messages:Queued", execution.InstanceId], Severity.Success);
            await state.RefreshAsync();
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
    }

    private async Task OpenTriggerAsync()
    {
        var state = PageState;
        if (_disposed || state?.Summary is not { } summary)
        {
            return;
        }

        var parameters = new DialogParameters<JobTriggerDialog>
        {
            { dialog => dialog.Definition, summary.Definition }
        };
        var dialog = await DialogService.ShowAsync<JobTriggerDialog>(
            L["Trigger:Title"],
            parameters,
            SmallDialogOptions);
        var result = await dialog.Result;
        if (_disposed || PageState != state)
        {
            return;
        }

        if (result is { Canceled: false, Data: Monica.JobScheduler.Models.Execution.JobExecutionInstance execution })
        {
            Snackbar.Add(L["Trigger:Queued", execution.InstanceId], Severity.Success);
            await state.RefreshAsync();
        }
    }

    private Task HandleStateChangedAsync(JobDefinitionDetailPageState state, int version) =>
        !IsCurrentStateVersion(version) || !ReferenceEquals(PageState, state)
            ? Task.CompletedTask
            : InvokeAsync(() =>
            {
                if (IsCurrentStateVersion(version) && ReferenceEquals(PageState, state))
                {
                    StateHasChanged();
                }
            });

    private bool IsCurrentStateVersion(int version) =>
        !_disposed && version == Volatile.Read(ref _stateVersion);

    private static DialogOptions SmallDialogOptions { get; } = new()
    {
        CloseButton = true,
        CloseOnEscapeKey = true,
        FullWidth = true,
        MaxWidth = MaxWidth.Small
    };

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Interlocked.Increment(ref _stateVersion);
        var state = PageState;
        var stateChangedHandler = _stateChangedHandler;
        PageState = null;
        _stateChangedHandler = null;
        if (state is not null)
        {
            if (stateChangedHandler is not null)
            {
                state.Changed -= stateChangedHandler;
            }

            await state.DisposeAsync();
        }
    }
}
