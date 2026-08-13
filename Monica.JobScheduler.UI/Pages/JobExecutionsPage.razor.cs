using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.UI.Components;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.UIJobScheduler.Executions.State;
using Monica.JobScheduler.UI.UIJobScheduler.Executions.Support;
using MudBlazor;

namespace Monica.JobScheduler.UI.Pages;

/// <summary>
/// Composes the durable execution ledger and routes operator interactions into component-owned state.
/// </summary>
public partial class JobExecutionsPage : IAsyncDisposable
{
    /// <summary>
    /// Gets the route for the execution ledger.
    /// </summary>
    public const string PAGE_URL = "/job-scheduler/executions";

    [Inject]
    private JobExecutionsStateFactory PageStateFactory { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private IStringLocalizer<JobSchedulerResource> L { get; set; } = null!;

    /// <summary>
    /// Gets or sets the optional comma-separated execution-state deep-link filter.
    /// </summary>
    [SupplyParameterFromQuery(Name = "states")]
    public string? InitialStates { get; set; }

    /// <summary>
    /// Gets or sets the optional execution identifier to filter and open as initial evidence.
    /// </summary>
    [SupplyParameterFromQuery(Name = "instanceId")]
    public string? InitialInstanceId { get; set; }

    private JobExecutionsPageState PageState { get; set; } = null!;
    private bool _disposed;
    private bool _initialDetailOpened;

    protected override async Task OnInitializedAsync()
    {
        PageState = PageStateFactory.CreatePageState();
        PageState.StateChanged += HandleStateChangedAsync;
        PageState.ApplyInitialQuery(InitialStates, InitialInstanceId);
        await PageState.InitializeAsync();
    }

    protected override async Task OnAfterRenderAsync(bool _)
    {
        var initialInstanceId = InitialInstanceId;
        if (_initialDetailOpened
            || _disposed
            || !PageState.AccessChecked
            || PageState.IsLoading
            || !PageState.IsAuthorized
            || string.IsNullOrWhiteSpace(initialInstanceId))
        {
            return;
        }

        _initialDetailOpened = true;
        await OpenDetailsAsync(initialInstanceId.Trim());
    }

    private void SetSelectedStates(IEnumerable<JobExecutionState>? states)
    {
        PageState.SetSelectedStates(states);
    }

    private string FormatState(JobExecutionState state) => L[$"ExecutionStates:{state}"];

    private string FormatTimeRange(ExecutionTimeRange range) => L[$"Executions:TimeRanges:{range}"];

    private string FormatSortField(JobExecutionSortField sortField) => L[$"Executions:SortFields:{sortField}"];

    private static string GetStateIcon(JobExecutionState state) => state switch
    {
        JobExecutionState.Queued => Icons.Material.Filled.Schedule,
        JobExecutionState.Running => Icons.Material.Filled.PlayCircle,
        JobExecutionState.Succeeded => Icons.Material.Filled.CheckCircle,
        JobExecutionState.Failed => Icons.Material.Filled.Error,
        JobExecutionState.Cancelled => Icons.Material.Filled.Cancel,
        JobExecutionState.Skipped => Icons.Material.Filled.SkipNext,
        _ => Icons.Material.Filled.Circle
    };

    private async Task OpenDetailsAsync(string instanceId)
    {
        var parameters = new DialogParameters<ExecutionDetailDialog>
        {
            { dialog => dialog.InstanceId, instanceId }
        };
        var dialog = await DialogService.ShowAsync<ExecutionDetailDialog>(
            L["ExecutionDetail:Title"],
            parameters,
            new DialogOptions
            {
                CloseButton = true,
                CloseOnEscapeKey = true,
                FullWidth = true,
                MaxWidth = MaxWidth.ExtraLarge
            });
        await dialog.Result;
        if (!_disposed)
        {
            await PageState.RefreshAsync();
        }
    }

    private async Task CancelAsync(JobExecutionInstance execution)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["Executions:Cancel:Title"],
            L["Executions:Cancel:Message", execution.InstanceId],
            yesText: L["Executions:Actions:Cancel"],
            cancelText: L["Common:Keep"]);
        if (confirmed != true || _disposed)
        {
            return;
        }

        var result = await PageState.RequestCancellationAsync(execution.InstanceId);
        if (!_disposed)
        {
            var feedback = ExecutionUiPresentation.GetCancellationFeedback(result, L);
            Snackbar.Add(feedback.Message, feedback.Severity);
        }
    }

    private Task HandleStateChangedAsync() => InvokeAsync(() =>
    {
        if (!_disposed)
        {
            StateHasChanged();
        }
    });

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (PageState is not null)
        {
            PageState.StateChanged -= HandleStateChangedAsync;
            await PageState.DisposeAsync();
        }
    }
}
