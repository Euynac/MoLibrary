using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Monica.Core.Results;
using Monica.JobScheduler.UI.Components;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.UIJobScheduler.Components;
using Monica.JobScheduler.UI.UIJobScheduler.State;
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

    private JobDefinitionDetailPageState? PageState { get; set; }
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
        var previousState = PageState;
        PageState = null;
        if (previousState is not null)
        {
            previousState.Changed -= HandleStateChangedAsync;
            await previousState.DisposeAsync();
        }

        if (_disposed)
        {
            return;
        }

        var state = PageStateFactory.Create(jobKey);
        state.Changed += HandleStateChangedAsync;
        PageState = state;
        await state.InitializeAsync();
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
            var disabled = !summary.Definition.IsDisabled;
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

            Snackbar.Add(
                disabled ? L["Catalog:Messages:Paused"] : L["Catalog:Messages:Resumed"],
                Severity.Success);
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

    private Task HandleStateChangedAsync() => _disposed ? Task.CompletedTask : InvokeAsync(StateHasChanged);

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
        var state = PageState;
        PageState = null;
        if (state is not null)
        {
            state.Changed -= HandleStateChangedAsync;
            await state.DisposeAsync();
        }
    }
}
