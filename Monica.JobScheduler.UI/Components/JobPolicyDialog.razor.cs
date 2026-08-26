using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Monica.Core.Results;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models.Definitions;
using Monica.JobScheduler.Models.Operations;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;
using Monica.JobScheduler.UI.UIJobScheduler.State;
using MudBlazor;

namespace Monica.JobScheduler.UI.Components;

/// <summary>
/// Hosts the full-width operator policy workbench and owns its optimistic save lifetime.
/// </summary>
public partial class JobPolicyDialog : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly object _operationSync = new();
    private TaskCompletionSource? _operationCompletion;
    private JobPolicyEditorState? _state;
    private bool _isSaving;
    private string? _error;
    private int _disposed;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private JobSchedulerFacade Facade { get; set; } = null!;

    [Inject]
    private IJobSchedulerUiAccess Access { get; set; } = null!;

    [Inject]
    private IStringLocalizer<JobSchedulerResource> L { get; set; } = null!;

    [Inject]
    private TimeProvider TimeProvider { get; set; } = null!;

    /// <summary>
    /// Gets the active definition used as the initial optimistic policy baseline.
    /// </summary>
    [Parameter, EditorRequired]
    public JobDefinition Definition { get; set; } = null!;

    /// <summary>
    /// Gets the optional authoritative operational projection used by schedule preview.
    /// </summary>
    [Parameter]
    public JobOperationalSummary? OperationalSummary { get; set; }

    /// <summary>
    /// Gets when the caller accepted the operational projection.
    /// </summary>
    [Parameter]
    public DateTimeOffset? ObservedAtUtc { get; set; }

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    private IReadOnlyList<IdentityFact> IdentityFacts => _state is null
        ? []
        :
        [
            new(L["Catalog:Columns:Owner"], _state.Definition.OwnerKey),
            new(L["Policy:Identity:PolicyRevision"], _state.Definition.Policy.ConcurrencyStamp),
            new(
                L["Policy:Identity:UpdatedAt"],
                _state.Definition.Policy.UpdatedAtUtc.UtcDateTime.ToString("u", CultureInfo.CurrentCulture)),
            new(
                L["Policy:Identity:LastObserved"],
                _state.Definition.LastObservedAtUtc.UtcDateTime.ToString("u", CultureInfo.CurrentCulture)),
            new(L["Policy:Identity:JobType"], L[$"JobTypes:{_state.Definition.Declaration.JobType}"]),
            new(
                L["Policy:Identity:ArgumentContract"],
                _state.Definition.Declaration.JobArgsKey ?? L["Common:NotAvailable"])
        ];

    protected override void OnParametersSet()
    {
        _state ??= JobPolicyEditorState.Create(
            Definition,
            OperationalSummary,
            ObservedAtUtc ?? TimeProvider.GetUtcNow());
    }

    private async Task SaveAsync()
    {
        if (_state is not { CanSave: true } state || !TryBeginSave())
        {
            return;
        }

        var cancellationToken = _lifetimeCancellation.Token;
        try
        {
            if (!await Access.IsAuthorizedAsync(cancellationToken))
            {
                if (!ShouldStop())
                {
                    _error = L["Access:DeniedDescription"];
                }

                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var result = await Facade.UpdatePolicyAsync(
                state.Definition.OwnerKey,
                state.Definition.Declaration.JobKey,
                state.CreateChange(),
                cancellationToken);
            if (ShouldStop())
            {
                return;
            }

            if (result.IsFailed(out var error, out var policy))
            {
                if (result.Status == ResStatus.Conflict)
                {
                    await RebaseAfterConflictAsync(state, cancellationToken);
                }
                else
                {
                    _error = error.Message;
                }

                return;
            }

            state.ClearConflict();
            MudDialog.Close(DialogResult.Ok(policy));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            CompleteSave();
        }
    }

    private async Task RebaseAfterConflictAsync(
        JobPolicyEditorState state,
        CancellationToken cancellationToken)
    {
        var latestResult = await Facade.GetOperationalSummaryAsync(
            state.Definition.Id,
            cancellationToken);
        if (ShouldStop())
        {
            return;
        }

        if (latestResult.IsFailed(out var loadError, out var latest) || latest is null)
        {
            _error = loadError?.Message ?? L["Policy:Conflict:ReloadFailed"];
            return;
        }

        state.RebaseAfterConflict(latest.Definition, latest, TimeProvider.GetUtcNow());
        _error = null;
    }

    private bool TryBeginSave()
    {
        lock (_operationSync)
        {
            if (IsDisposed || _operationCompletion is not null)
            {
                return false;
            }

            _operationCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _isSaving = true;
            _error = null;
            return true;
        }
    }

    private void CompleteSave()
    {
        TaskCompletionSource? completion;
        lock (_operationSync)
        {
            completion = _operationCompletion;
            _operationCompletion = null;
            if (!IsDisposed)
            {
                _isSaving = false;
            }
        }

        completion?.TrySetResult();
    }

    private bool ShouldStop() => IsDisposed || _lifetimeCancellation.IsCancellationRequested;

    private void SetDisabledOverride(bool? value) => _state!.DisabledOverride = value;

    private void SetDisplayName(string? value) => _state!.SetDisplayName(value);

    private void ResetDisplayName() => _state!.ResetDisplayName();

    private void SetMaxConcurrency(int? value) => _state!.MaxConcurrencyOverride = value;

    private void SetRetryCount(int? value) => _state!.RetryCountOverride = value;

    private void SetMaxRecords(int? value) => _state!.MaxRetainedHistoryRecords = value;

    private void SetMaxRetentionDays(int? value) => _state!.MaxRetentionDays = value;

    private void ResetMaxRetentionDays() => _state!.MaxRetentionDays = null;

    private void EnableTimeoutOverride() => _state!.EnableTimeoutOverride();

    private void ResetTimeout() => _state!.ResetTimeout();

    private void ResetAll()
    {
        _state!.ResetAll();
        _error = null;
    }

    private void DraftChanged() => _error = null;

    private void DiscardScheduleDraft()
    {
        _state!.DiscardScheduleDraft();
        _error = null;
    }

    private string EnabledLabel(bool enabled) => enabled
        ? L["Policy:Values:Enabled"]
        : L["Policy:Values:Disabled"];

    private string DisplayText(string? value) => string.IsNullOrWhiteSpace(value)
        ? L["Common:NotAvailable"]
        : value;

    private static string FormatDuration(TimeSpan value) => value.Ticks % TimeSpan.TicksPerSecond == 0
        ? value.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture)
        : value.ToString(@"d\.hh\:mm\:ss\.fffffff", CultureInfo.InvariantCulture);

    private void Cancel()
    {
        if (!IsDisposed)
        {
            MudDialog.Cancel();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _lifetimeCancellation.CancelAsync();
        Task activeOperation;
        lock (_operationSync)
        {
            activeOperation = _operationCompletion?.Task ?? Task.CompletedTask;
        }

        await activeOperation.ConfigureAwait(false);
        _lifetimeCancellation.Dispose();
    }

    private sealed record IdentityFact(string Label, string Value);
}
