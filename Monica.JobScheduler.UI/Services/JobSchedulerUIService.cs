using Monica.JobScheduler.Models;
using Monica.JobScheduler.UI.Models;
using Monica.Core.Results;
using Monica.JobScheduler.Facades;

namespace Monica.JobScheduler.UI.Services;

/// <summary>
/// JobScheduler UI facade service
/// Provide a unified interface for UI component calls
/// </summary>
public class JobSchedulerUIService(
    JobSchedulerFacade apiService,
    JobDefinitionQueryService definitionQuery,
    JobInstanceQueryService instanceQuery,
    JobStatisticsService statisticsService,
    JobHealthMetricsService healthMetricsService)
{
    // Job Definition Operations
    public Task<ResPaged<JobDefinition>> GetJobDefinitionsAsync(
        JobDefinitionFilterRequest filter,
        CancellationToken cancellationToken = default)
        => definitionQuery.GetJobDefinitionsAsync(filter, cancellationToken);

    public Task<Res<JobDefinition?>> GetJobDefinitionAsync(
        string jobKey,
        CancellationToken cancellationToken = default)
        => definitionQuery.GetJobDefinitionAsync(jobKey, cancellationToken);

    public Task<ResPaged<JobDefinitionWithLastExecution>> GetJobDefinitionsWithLastExecutionAsync(
        JobDefinitionFilterRequest filter,
        CancellationToken cancellationToken = default)
        => definitionQuery.GetJobDefinitionsWithLastExecutionAsync(filter, cancellationToken);

    // Job Instance Operations
    public Task<ResPaged<JobInstance>> GetJobInstancesAsync(
        JobInstanceFilterRequest filter,
        CancellationToken cancellationToken = default)
        => instanceQuery.GetJobInstancesAsync(filter, cancellationToken);

    public Task<Res<JobInstance?>> GetJobInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
        => instanceQuery.GetJobInstanceAsync(instanceId, cancellationToken);

    public Task<Res<List<JobInstance>>> GetRunningInstancesAsync(
        string jobKey,
        CancellationToken cancellationToken = default)
        => instanceQuery.GetRunningInstancesAsync(jobKey, cancellationToken);

    // Job Control Operations
    public Task<Res> PauseJobAsync(
        string jobKey,
        CancellationToken cancellationToken = default)
        => apiService.PauseRecurringJobAsync(jobKey, cancellationToken);

    public Task<Res> ResumeJobAsync(
        string jobKey,
        CancellationToken cancellationToken = default)
        => apiService.ResumeRecurringJobAsync(jobKey, cancellationToken);

    public Task<Res<string>> ExecuteJobAsync(
        string jobKey,
        object? jobArgs = null,
        CancellationToken cancellationToken = default)
        => apiService.CreateJobInstanceAsync(jobKey, jobArgs, cancellationToken);

    public Task<Res> CancelInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
        => apiService.CancelJobInstanceAsync(instanceId, cancellationToken);

    public Task<Res> UpdateJobConfigAsync(
        JobDefinition definition,
        CancellationToken cancellationToken = default)
        => apiService.UpdateJobConfigAsync(definition.JobKey, definition, cancellationToken);

    // Statistics and Metrics
    public Task<Res<JobStatistics>> GetStatisticsAsync(
        string jobKey,
        CancellationToken cancellationToken = default)
        => statisticsService.CalculateStatisticsAsync(jobKey, cancellationToken);

    public Task<Res<JobHealthMetrics>> GetHealthMetricsAsync(
        string jobKey,
        CancellationToken cancellationToken = default)
        => healthMetricsService.CalculateHealthMetricsAsync(jobKey, cancellationToken);

    // Batch Operations
    public Task<Res<BatchJobOperationResult>> BatchPauseJobsAsync(
        IReadOnlyList<string> jobKeys,
        CancellationToken cancellationToken = default)
        => apiService.BatchUpdateJobStateAsync(jobKeys, isDisabled: true, cancellationToken);

    public Task<Res<BatchJobOperationResult>> BatchResumeJobsAsync(
        IReadOnlyList<string> jobKeys,
        CancellationToken cancellationToken = default)
        => apiService.BatchUpdateJobStateAsync(jobKeys, isDisabled: false, cancellationToken);

    // History Cleanup
    public Task<Res<HistoryCleanupResult>> TriggerHistoryCleanupAsync(
        CancellationToken cancellationToken = default)
        => apiService.TriggerHistoryCleanupAsync(cancellationToken);
}
