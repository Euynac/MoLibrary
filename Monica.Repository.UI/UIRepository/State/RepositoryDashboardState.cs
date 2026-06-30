using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core.Results;
using Monica.Modules;
using Monica.Repository.Facades;
using Monica.Repository.Persistence.Models;

namespace Monica.Repository.UI.UIRepository.State;

/// <summary>
/// Page state for the Repository diagnostics dashboard.
/// </summary>
public sealed class RepositoryDashboardState(
    RepositoryDiagnosticsFacade facade,
    IOptions<ModuleRepositoryUIOption> options,
    IHostEnvironment hostEnvironment)
{
    /// <summary>
    /// Registered DbContext snapshots.
    /// </summary>
    public IReadOnlyList<RepositoryDbContextSnapshot> Contexts { get; private set; } = [];

    /// <summary>
    /// Currently selected DbContext snapshot.
    /// </summary>
    public RepositoryDbContextSnapshot? SelectedContext { get; private set; }

    /// <summary>
    /// Activity result for the selected DbContext.
    /// </summary>
    public RepositoryActivityResult? Activity { get; private set; }

    /// <summary>
    /// Migration status for the selected DbContext.
    /// </summary>
    public RepositoryMigrationStatus? MigrationStatus { get; private set; }

    /// <summary>
    /// Last migration update results.
    /// </summary>
    public IReadOnlyList<RepositoryMigrationUpdateResult> UpdateResults { get; private set; } = [];

    /// <summary>
    /// Last page-level load error.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Whether the dashboard is loading the registered context list.
    /// </summary>
    public bool IsLoadingContexts { get; private set; }

    /// <summary>
    /// Whether the dashboard is loading details for the selected context.
    /// </summary>
    public bool IsLoadingDetails { get; private set; }

    /// <summary>
    /// Whether a migration update operation is running.
    /// </summary>
    public bool IsUpdatingMigrations { get; private set; }

    /// <summary>
    /// Whether full connection strings should be included in context snapshots.
    /// </summary>
    public bool RevealConnectionStrings { get; private set; }

    /// <summary>
    /// Whether migration update actions are currently enabled.
    /// </summary>
    public bool MigrationUpdatesEnabled =>
        !hostEnvironment.IsProduction() || options.Value.AllowMigrationUpdateInProduction;

    /// <summary>
    /// Migration command timeout configured for UI-triggered updates.
    /// </summary>
    public TimeSpan MigrationCommandTimeout => options.Value.MigrationCommandTimeout;

    /// <summary>
    /// Initializes dashboard data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshContextsAsync(keepSelection: false, cancellationToken);
    }

    /// <summary>
    /// Reloads all registered DbContext snapshots.
    /// </summary>
    /// <param name="keepSelection">Whether to preserve the active selection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RefreshContextsAsync(bool keepSelection = true, CancellationToken cancellationToken = default)
    {
        var selectedId = keepSelection ? SelectedContext?.Registration.ContextId : null;
        IsLoadingContexts = true;
        ErrorMessage = null;

        try
        {
            var result = await facade.GetRegisteredContextsAsync(RevealConnectionStrings, cancellationToken);
            if (result.IsFailed(out var error, out var snapshots))
            {
                Contexts = [];
                SelectedContext = null;
                Activity = null;
                MigrationStatus = null;
                ErrorMessage = error.Message;
                return;
            }

            Contexts = snapshots;
            SelectedContext = SelectSnapshot(selectedId) ?? Contexts.FirstOrDefault();
        }
        finally
        {
            IsLoadingContexts = false;
        }

        await LoadSelectedDetailsAsync(cancellationToken);
    }

    /// <summary>
    /// Selects a DbContext and loads its details.
    /// </summary>
    /// <param name="contextId">Selected DbContext identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SelectContextAsync(string contextId, CancellationToken cancellationToken = default)
    {
        SelectedContext = SelectSnapshot(contextId);
        UpdateResults = [];
        await LoadSelectedDetailsAsync(cancellationToken);
    }

    /// <summary>
    /// Toggles full connection-string visibility and refreshes context snapshots.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ToggleConnectionStringRevealAsync(CancellationToken cancellationToken = default)
    {
        RevealConnectionStrings = !RevealConnectionStrings;
        await RefreshContextsAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Applies pending migrations for the selected DbContext.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The update result when the facade call completes successfully.</returns>
    public async Task<RepositoryMigrationUpdateResult?> UpdateSelectedMigrationAsync(
        CancellationToken cancellationToken = default)
    {
        if (SelectedContext is null || !MigrationUpdatesEnabled)
        {
            return null;
        }

        IsUpdatingMigrations = true;
        ErrorMessage = null;

        try
        {
            var result = await facade.UpdateMigrationAsync(
                SelectedContext.Registration.ContextId,
                MigrationCommandTimeout,
                cancellationToken);

            if (result.IsFailed(out var error, out var updateResult))
            {
                ErrorMessage = error.Message;
                return null;
            }

            UpdateResults = [updateResult];
            await RefreshContextsAsync(cancellationToken: cancellationToken);
            return updateResult;
        }
        finally
        {
            IsUpdatingMigrations = false;
        }
    }

    /// <summary>
    /// Applies pending migrations for all registered DbContexts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Per-context update results when the facade call completes successfully.</returns>
    public async Task<IReadOnlyList<RepositoryMigrationUpdateResult>> UpdateAllPendingMigrationsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!MigrationUpdatesEnabled)
        {
            return [];
        }

        IsUpdatingMigrations = true;
        ErrorMessage = null;

        try
        {
            var result = await facade.UpdateAllPendingMigrationsAsync(MigrationCommandTimeout, cancellationToken);
            if (result.IsFailed(out var error, out var updateResults))
            {
                ErrorMessage = error.Message;
                return [];
            }

            UpdateResults = updateResults;
            await RefreshContextsAsync(cancellationToken: cancellationToken);
            return updateResults;
        }
        finally
        {
            IsUpdatingMigrations = false;
        }
    }

    private async Task LoadSelectedDetailsAsync(CancellationToken cancellationToken)
    {
        if (SelectedContext is null)
        {
            Activity = null;
            MigrationStatus = null;
            return;
        }

        IsLoadingDetails = true;
        ErrorMessage = null;

        try
        {
            var contextId = SelectedContext.Registration.ContextId;
            var migrationResult = await facade.GetMigrationStatusAsync(contextId, cancellationToken);
            MigrationStatus = migrationResult.IsFailed(out var migrationError, out var migrationStatus)
                ? new RepositoryMigrationStatus
                {
                    Registration = SelectedContext.Registration,
                    ErrorMessage = migrationError.Message
                }
                : migrationStatus;

            var activityResult = await facade.GetActivityAsync(contextId, cancellationToken);
            Activity = activityResult.IsFailed(out var activityError, out var activity)
                ? new RepositoryActivityResult
                {
                    Registration = SelectedContext.Registration,
                    Support = RepositoryActivitySupport.Failed,
                    Message = activityError.Message
                }
                : activity;
        }
        finally
        {
            IsLoadingDetails = false;
        }
    }

    private RepositoryDbContextSnapshot? SelectSnapshot(string? contextId)
    {
        if (string.IsNullOrWhiteSpace(contextId))
        {
            return null;
        }

        return Contexts.FirstOrDefault(context =>
            string.Equals(context.Registration.ContextId, contextId, StringComparison.Ordinal));
    }
}
