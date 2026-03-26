using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Monica.DevOps.Git.Facades;
using Monica.DevOps.Git.Models;
using Monica.DevOps.Localization;
using Monica.Tool.MoResponse;
using MudBlazor;

namespace Monica.DevOps.Git.Pages;

public partial class UIGitRepositoriesPage : IAsyncDisposable
{
    public const string PAGE_URL = "/git-repositories";

    private static readonly TimeSpan OperationPollInterval = TimeSpan.FromMilliseconds(400);
    private const string LoadFailedMessageKey = "GitDashboard:Messages:LoadFailed";
    private const string OperationFailedMessageKey = "GitDashboard:Messages:OperationFailed";

    [Inject]
    private GitFacade GitService { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    [Inject]
    private IStringLocalizer<GitResource> L { get; set; } = default!;

    private bool _isLoading;
    private bool _isOperating;
    private bool _isDisposed;
    private readonly CancellationTokenSource _disposeCts = new();
    private List<GitRepositoryStatus> _repositories = [];
    private List<GitCredentialInfo> _credentials = [];
    private List<GitCredentialResolverInfo> _resolvers = [];
    private GitRepositoryStatus? _selectedRepository;

    private bool IsBusy => _isLoading || _isOperating;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await LoadAsync();
    }

    public ValueTask DisposeAsync()
    {
        _isDisposed = true;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task RefreshAsync()
    {
        return LoadAsync();
    }

    private async Task LoadAsync(bool showErrors = true)
    {
        if (_isDisposed || _isLoading)
        {
            return;
        }

        _isLoading = true;
        await RefreshViewAsync();

        try
        {
            if (!await LoadRepositoriesAsync(showErrors))
            {
                return;
            }

            await LoadMetadataAsync(showErrors);
        }
        finally
        {
            _isLoading = false;
            await RefreshViewAsync();
        }
    }

    private async Task<bool> LoadRepositoriesAsync(bool showErrors)
    {
        var response = await GitService.GetRepositoriesAsync();
        if (!TryGetResponseValue(response, showErrors, LoadFailedMessageKey, out var repositories))
        {
            return false;
        }

        ApplyRepositories(repositories);
        return true;
    }

    private async Task<bool> LoadMetadataAsync(bool showErrors)
    {
        var credentialsResponse = await GitService.GetCredentialsAsync();
        if (!TryGetResponseValue(credentialsResponse, showErrors, LoadFailedMessageKey, out var credentials))
        {
            return false;
        }

        var resolversResponse = await GitService.GetCredentialResolversAsync();
        if (!TryGetResponseValue(resolversResponse, showErrors, LoadFailedMessageKey, out var resolvers))
        {
            return false;
        }

        _credentials = credentials.ToList();
        _resolvers = resolvers.ToList();
        return true;
    }

    private void ApplyRepositories(IReadOnlyList<GitRepositoryStatus> repositories)
    {
        _repositories = repositories.ToList();

        if (_selectedRepository is null)
        {
            _selectedRepository = _repositories.FirstOrDefault();
            return;
        }

        _selectedRepository = _repositories.FirstOrDefault(x => x.Id == _selectedRepository.Id)
            ?? _repositories.FirstOrDefault();
    }

    private async Task<bool> RefreshRepositoryAsync(string repositoryId, bool showErrors = false)
    {
        var response = await GitService.GetRepositoryAsync(repositoryId);
        if (!TryGetResponseValue(response, showErrors, LoadFailedMessageKey, out var repository))
        {
            return false;
        }

        UpdateRepositoryPreview(repositoryId, _ => repository);
        await RefreshViewAsync();
        return true;
    }

    private void SelectRepository(string repositoryId)
    {
        _selectedRepository = _repositories.FirstOrDefault(x => x.Id == repositoryId);
    }

    private async Task SyncRepositoryAsync(string repositoryId)
    {
        PreviewRepositorySync(repositoryId);
        await RefreshViewAsync();

        var syncTask = GitService.SyncRepositoryAsync(repositoryId);
        var response = await RunOperationAsync(
            syncTask,
            (task, cancellationToken) => TrackOperationAsync(
                task,
                () => RefreshRepositoryAsync(repositoryId, showErrors: false),
                cancellationToken));

        if (response is null)
        {
            return;
        }

        if (response.IsFailed(out var error, out var result))
        {
            ShowError(OperationFailedMessageKey, error);
        }
        else
        {
            Snackbar.Add(
                result.Success
                    ? L["GitDashboard:Messages:SyncRepositorySuccess", repositoryId]
                    : L[OperationFailedMessageKey, result.Message],
                result.Success ? Severity.Success : Severity.Error);
        }

        await RefreshRepositoryAsync(repositoryId);
    }

    private async Task DeleteRepositoryAsync(string repositoryId)
    {
        if (!await ConfirmDeleteRepositoryAsync(repositoryId))
        {
            return;
        }

        PreviewRepositoryDeletion(repositoryId);
        await RefreshViewAsync();

        var deleteTask = GitService.DeleteRepositoryAsync(repositoryId);
        var response = await RunOperationAsync(
            deleteTask,
            (task, cancellationToken) => TrackOperationAsync(
                task,
                () => RefreshRepositoryAsync(repositoryId, showErrors: false),
                cancellationToken));

        if (response is null)
        {
            return;
        }

        if (response.IsFailed(out var error, out var result))
        {
            ShowError(OperationFailedMessageKey, error);
        }
        else
        {
            Snackbar.Add(
                result.Success
                    ? L["GitDashboard:Messages:DeleteRepositorySuccess", repositoryId]
                    : L[OperationFailedMessageKey, result.Message],
                result.Success ? Severity.Success : Severity.Error);
        }

        await RefreshRepositoryAsync(repositoryId, showErrors: true);
    }

    private async Task SyncAllAsync()
    {
        PreviewAllRepositoriesSync();
        await RefreshViewAsync();

        var syncTask = GitService.SyncAllAsync();
        var response = await RunOperationAsync(syncTask, (task, cancellationToken) =>
            TrackOperationAsync(task, RefreshAllRepositoriesPreviewAsync, cancellationToken));

        if (response is null)
        {
            return;
        }

        if (response.IsFailed(out var error, out var results))
        {
            ShowError(OperationFailedMessageKey, error);
        }
        else
        {
            var failures = results.Where(x => !x.Success).ToList();
            Snackbar.Add(
                failures.Count == 0
                    ? L["GitDashboard:Messages:SyncAllSuccess"]
                    : L[OperationFailedMessageKey, string.Join("; ", failures.Select(x => x.Message))],
                failures.Count == 0 ? Severity.Success : Severity.Warning);
        }

        await LoadAsync(showErrors: false);
    }

    private async Task<Res<TResult>?> RunOperationAsync<TResult>(
        Task<Res<TResult>> operationTask,
        Func<Task<Res<TResult>>, CancellationToken, Task>? tracker = null)
    {
        _isOperating = true;
        await RefreshViewAsync();

        try
        {
            if (tracker is not null)
            {
                await tracker(operationTask, _disposeCts.Token);
                if (_isDisposed)
                {
                    return null;
                }
            }

            return await operationTask;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _isOperating = false;
            await RefreshViewAsync();
        }
    }

    private async Task TrackOperationAsync(
        Task operationTask,
        Func<Task> refreshAsync,
        CancellationToken cancellationToken)
    {
        while (!operationTask.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(OperationPollInterval, cancellationToken);
            if (operationTask.IsCompleted)
            {
                break;
            }

            await refreshAsync();
        }
    }

    private async Task RefreshAllRepositoriesPreviewAsync()
    {
        await LoadRepositoriesAsync(showErrors: false);
        await RefreshViewAsync();
    }

    private void PreviewAllRepositoriesSync()
    {
        UpdateAllRepositoriesPreview(
            GitRepositorySyncState.Syncing,
            GitRepositoryProgressStages.Preparing,
            null,
            L["GitDashboard:Messages:SyncAllInProgress"]);
    }

    private void PreviewRepositorySync(string repositoryId)
    {
        UpdateRepositoryPreview(repositoryId, repository => CopyRepositoryStatus(
            repository,
            state: GitRepositorySyncState.Syncing,
            progressStage: GitRepositoryProgressStages.Preparing,
            progressPercent: null,
            lastSyncMessage: L["GitDashboard:Messages:SyncInProgress", repositoryId],
            lastError: null));
    }

    private void PreviewRepositoryDeletion(string repositoryId)
    {
        UpdateRepositoryPreview(repositoryId, repository => CopyRepositoryStatus(
            repository,
            state: GitRepositorySyncState.Syncing,
            progressStage: GitRepositoryProgressStages.Deleting,
            progressPercent: null,
            lastSyncMessage: L["GitDashboard:ProgressStages:Deleting"],
            lastError: null));
    }

    private async Task<bool> ConfirmDeleteRepositoryAsync(string repositoryId)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["GitDashboard:Dialogs:DeleteLocalRepository:Title"],
            L["GitDashboard:Dialogs:DeleteLocalRepository:Message", repositoryId],
            yesText: L["GitDashboard:Dialogs:DeleteLocalRepository:Confirm"],
            cancelText: L["GitDashboard:Dialogs:DeleteLocalRepository:Cancel"]);

        return confirmed == true;
    }

    private bool TryGetResponseValue<T>(
        Res<T> response,
        bool showErrors,
        string errorMessageKey,
        [MaybeNullWhen(false)] out T value)
    {
        if (!response.IsFailed(out var error, out value))
        {
            return true;
        }

        if (showErrors)
        {
            ShowError(errorMessageKey, error);
        }

        return false;
    }

    private void ShowError(string messageKey, string error)
    {
        Snackbar.Add(L[messageKey, error], Severity.Error);
    }

    private Task RefreshViewAsync()
    {
        return _isDisposed ? Task.CompletedTask : InvokeAsync(StateHasChanged);
    }

    private void UpdateAllRepositoriesPreview(
        GitRepositorySyncState state,
        string? progressStage = null,
        double? progressPercent = null,
        string? message = null)
    {
        _repositories = _repositories
            .Select(repository => CopyRepositoryStatus(
                repository,
                state: state,
                progressStage: progressStage,
                progressPercent: progressPercent,
                lastSyncMessage: message,
                lastError: null))
            .ToList();

        if (_selectedRepository is not null)
        {
            _selectedRepository = _repositories.FirstOrDefault(x => x.Id == _selectedRepository.Id)
                ?? _selectedRepository;
        }
    }

    private void UpdateRepositoryPreview(string repositoryId, Func<GitRepositoryStatus, GitRepositoryStatus> updater)
    {
        var index = _repositories.FindIndex(x => string.Equals(x.Id, repositoryId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        var updated = updater(_repositories[index]);
        _repositories[index] = updated;

        if (_selectedRepository is not null
            && string.Equals(_selectedRepository.Id, repositoryId, StringComparison.OrdinalIgnoreCase))
        {
            _selectedRepository = updated;
        }
    }

    private static GitRepositoryStatus CopyRepositoryStatus(
        GitRepositoryStatus repository,
        GitRepositorySyncState? state = null,
        bool? isAvailable = null,
        bool? isDirty = null,
        string? resolvedBranch = null,
        string? currentCommit = null,
        string? progressStage = null,
        double? progressPercent = null,
        long? workingDirectorySizeBytes = null,
        string? lastSyncMessage = null,
        string? lastError = null)
    {
        return new GitRepositoryStatus
        {
            Id = repository.Id,
            Provider = repository.Provider,
            RemoteUrl = repository.RemoteUrl,
            LocalPath = repository.LocalPath,
            ResolvedLocalPath = repository.ResolvedLocalPath,
            ConfiguredBranch = repository.ConfiguredBranch,
            ResolvedBranch = resolvedBranch ?? repository.ResolvedBranch,
            CredentialId = repository.CredentialId,
            State = state ?? repository.State,
            IsAvailable = isAvailable ?? repository.IsAvailable,
            IsDirty = isDirty ?? repository.IsDirty,
            CurrentCommit = currentCommit ?? repository.CurrentCommit,
            ProgressStage = progressStage ?? repository.ProgressStage,
            ProgressPercent = progressPercent ?? repository.ProgressPercent,
            WorkingDirectorySizeBytes = workingDirectorySizeBytes ?? repository.WorkingDirectorySizeBytes,
            LastSyncAtUtc = repository.LastSyncAtUtc,
            LastSyncMessage = lastSyncMessage ?? repository.LastSyncMessage,
            LastError = lastError ?? repository.LastError
        };
    }

    private string GetStateText(GitRepositorySyncState state)
    {
        return state switch
        {
            GitRepositorySyncState.NotReady => L["GitDashboard:States:NotReady"],
            GitRepositorySyncState.Syncing => L["GitDashboard:States:Syncing"],
            GitRepositorySyncState.Ready => L["GitDashboard:States:Ready"],
            GitRepositorySyncState.Failed => L["GitDashboard:States:Failed"],
            _ => state.ToString()
        };
    }

    private string GetProgressText(GitRepositoryStatus repository)
    {
        var stage = GetProgressStageText(repository.ProgressStage);
        if (string.IsNullOrWhiteSpace(stage))
        {
            return L["GitDashboard:States:None"];
        }

        return repository.ProgressPercent is double progressPercent
            ? $"{stage} ({progressPercent:0}%)"
            : stage;
    }

    private string GetProgressStageText(string? progressStage)
    {
        return progressStage switch
        {
            GitRepositoryProgressStages.Preparing => L["GitDashboard:ProgressStages:Preparing"],
            GitRepositoryProgressStages.Cloning => L["GitDashboard:ProgressStages:Cloning"],
            GitRepositoryProgressStages.Fetching => L["GitDashboard:ProgressStages:Fetching"],
            GitRepositoryProgressStages.Checkout => L["GitDashboard:ProgressStages:Checkout"],
            GitRepositoryProgressStages.Deleting => L["GitDashboard:ProgressStages:Deleting"],
            _ => string.Empty
        };
    }

    private static bool IsProgressIndeterminate(GitRepositoryStatus repository)
    {
        return repository.State == GitRepositorySyncState.Syncing
            && !repository.ProgressPercent.HasValue;
    }

    private static double GetProgressValue(GitRepositoryStatus repository)
    {
        return repository.ProgressPercent ?? 0d;
    }

    private static Color GetStateColor(GitRepositorySyncState state)
    {
        return state switch
        {
            GitRepositorySyncState.Ready => Color.Success,
            GitRepositorySyncState.Syncing => Color.Info,
            GitRepositorySyncState.Failed => Color.Error,
            _ => Color.Default
        };
    }

    private string BoolText(bool value)
    {
        return value ? L["GitDashboard:States:Yes"] : L["GitDashboard:States:No"];
    }

    private string GetDisplay(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? L["GitDashboard:States:None"] : value;
    }

    private static string ShortSha(string? sha)
    {
        return string.IsNullOrWhiteSpace(sha)
            ? string.Empty
            : sha.Length <= 8
                ? sha
                : sha[..8];
    }

    private static string FormatDate(DateTimeOffset? dateTimeOffset)
    {
        return dateTimeOffset?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} {units[unitIndex]}"
            : $"{size:0.##} {units[unitIndex]}";
    }
}
