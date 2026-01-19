using Microsoft.Extensions.Options;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Metadata;
using MoLibrary.JobScheduler.UI.Models;
using MoLibrary.JobScheduler.UI.Modules;

namespace MoLibrary.JobScheduler.UI.Services;

/// <summary>
/// Coordinates efficient data loading for the dashboard.
/// Minimizes database queries by batch-fetching all needed data.
/// </summary>
public class DashboardDataLoader(
    IMoJobMetadataRepository metadataRepository,
    IJobDefinitionCacheService cacheService,
    IOptions<ModuleJobSchedulerUIOption> uiOptions)
{
    private readonly ModuleJobSchedulerUIOption _options = uiOptions.Value;

    /// <summary>
    /// Loads all data needed for dashboard in minimal database queries.
    /// Query count: ~3 queries regardless of job count (vs N+1 before)
    /// </summary>
    public async Task<DashboardDataContext> LoadAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var metricsStartTime = now - _options.HealthMetricsWindow;

        // Query 1: Get all job definitions (from cache)
        var definitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);

        // Build lookup maps
        var jobNameMap = definitions.ToDictionary(d => d.JobKey, d => d.JobName);
        var jobConfigMap = definitions.ToDictionary(d => d.JobKey, d => d);

        // Query 2 & 3: Execute in parallel
        var stateDistributionTask = metadataRepository.GetStateStatisticsAsync(
            metricsStartTime, now, cancellationToken);

        // Single query with projection - load all instances needed for analysis
        var instancesTask = LoadAllInstancesAsync(metricsStartTime, cancellationToken);

        await Task.WhenAll(stateDistributionTask, instancesTask);

        return new DashboardDataContext
        {
            Definitions = definitions,
            JobNameMap = jobNameMap,
            JobConfigMap = jobConfigMap,
            StateDistribution = await stateDistributionTask,
            AllInstances = await instancesTask,
            MetricsStartTime = metricsStartTime,
            MetricsEndTime = now
        };
    }

    private async Task<List<InstanceProjection>> LoadAllInstancesAsync(
        DateTime startTime, CancellationToken cancellationToken)
    {
        // Load instances from metrics window for skip rate analysis, recent activities, etc.
        // Also need to load Processing instances (which may have started before the window)
        var query = new JobInstanceQuery
        {
            CreatedAfter = startTime,
            PageNumber = 1,
            PageSize = int.MaxValue,
            SortBy = "CreatedAt",
            SortDescending = true
        };

        // Use projection to only load needed fields (no StateHistory, JobArgs, etc.)
        var result = await metadataRepository.QueryInstancesAsync(
            query,
            i => new InstanceProjection(
                i.InstanceId,
                i.JobKey,
                i.State,
                i.CreatedAt,
                i.StartedAt,
                i.CompletedAt),
            cancellationToken);

        return result.Items;
    }
}
