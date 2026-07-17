using Microsoft.Extensions.Localization;
using Monica.Configuration.UI.Dialogs;
using Monica.Configuration.UI.Localization;
using Monica.Configuration.UI.Models;
using MudBlazor;

namespace Monica.Configuration.UI.Support;

internal static class ConfigurationRollbackCompletionPresenter
{
    public static async Task ShowAsync(
        ConfigurationHistoryRollbackDialogResult rollback,
        IDialogService dialogService,
        ISnackbar snackbar,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        if (rollback.PostCommitIssues.Count == 0)
        {
            snackbar.Add(localizer["Rollback:Success"], Severity.Success);
            return;
        }

        var summary = localizer["Rollback:AppliedWithIssues"].Value;
        snackbar.Add(summary, Severity.Warning);

        var parameters = new DialogParameters<ConfigurationPostCommitIssuesDialog>();
        parameters.Add(dialog => dialog.Issues, rollback.PostCommitIssues);
        parameters.Add(dialog => dialog.SummaryText, summary);
        var dialog = await dialogService.ShowAsync<ConfigurationPostCommitIssuesDialog>(
            localizer["Dialogs:PostCommitIssues:Title"],
            parameters,
            ConfigurationPostCommitIssuesDialog.DefaultOptions);
        await dialog.Result;
    }
}
