using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.UI.UIJobScheduler.Shared;

/// <summary>
/// Builds owner-aware scheduler URLs from the same identity used by the store and detail page.
/// </summary>
internal static class JobSchedulerUiUrls
{
    private const string JOB_DEFINITION_PATH = "/job-scheduler/catalog";

    /// <summary>
    /// Builds the detail URL for one owner-scoped job definition.
    /// </summary>
    internal static string JobDefinition(JobId jobId) =>
        $"{JOB_DEFINITION_PATH}/{Uri.EscapeDataString(jobId.OwnerKey)}/{Uri.EscapeDataString(jobId.JobKey)}";
}
