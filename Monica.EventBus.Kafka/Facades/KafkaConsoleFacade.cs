using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.EventBus.Kafka.Models;
using Monica.EventBus.Kafka.Services;

namespace Monica.EventBus.Kafka.Facades;

/// <summary>
/// Host-facing Kafka console entry point used by Minimal APIs and Blazor UI.
/// </summary>
public sealed class KafkaConsoleFacade(
    KafkaIntegrationService integrationService,
    KafkaClusterService clusterService,
    KafkaDashboardService dashboardService,
    KafkaTopicService topicService,
    KafkaConsumerGroupService consumerGroupService,
    KafkaPerformanceService performanceService)
{
    /// <summary>
    /// Gets the current Kafka EventBus integration state.
    /// </summary>
    public Task<Res<KafkaIntegrationSnapshot>> GetIntegrationAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => integrationService.GetSnapshotAsync(cancellationToken), "Failed to load Kafka integration state");
    }

    /// <summary>
    /// Gets Kafka clusters visible to the console.
    /// </summary>
    public Task<Res<IReadOnlyList<KafkaClusterSummary>>> ListClustersAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => clusterService.ListClustersAsync(cancellationToken), "Failed to load Kafka clusters");
    }

    /// <summary>
    /// Creates or updates a Kafka cluster.
    /// </summary>
    public Task<Res<KafkaClusterSummary>> UpsertClusterAsync(KafkaClusterUpsertRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() =>
        {
            ArgumentNullException.ThrowIfNull(request);
            return clusterService.UpsertClusterAsync(request.Cluster, cancellationToken);
        }, "Failed to save Kafka cluster");
    }

    /// <summary>
    /// Deletes a Kafka cluster saved through the console.
    /// </summary>
    public Task<Res> DeleteClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => clusterService.DeleteClusterAsync(clusterId, cancellationToken), "Kafka cluster deleted", "Failed to delete Kafka cluster");
    }

    /// <summary>
    /// Tests direct Kafka connectivity for a cluster.
    /// </summary>
    public Task<Res<KafkaClusterSummary>> TestClusterAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => clusterService.TestConnectionAsync(clusterId, cancellationToken), "Failed to test Kafka cluster");
    }

    /// <summary>
    /// Gets a dashboard aggregate.
    /// </summary>
    public Task<Res<KafkaDashboardSnapshot>> GetDashboardAsync(string? clusterId = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => dashboardService.GetDashboardAsync(clusterId, cancellationToken), "Failed to load Kafka dashboard");
    }

    /// <summary>
    /// Lists topics in a cluster.
    /// </summary>
    public Task<Res<IReadOnlyList<KafkaTopicSummary>>> ListTopicsAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => topicService.ListTopicsAsync(clusterId, cancellationToken), "Failed to load Kafka topics");
    }

    /// <summary>
    /// Creates a topic.
    /// </summary>
    public Task<Res> CreateTopicAsync(KafkaTopicCreateRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() =>
        {
            ArgumentNullException.ThrowIfNull(request);
            return topicService.CreateTopicAsync(request, cancellationToken);
        }, "Kafka topic created", "Failed to create Kafka topic");
    }

    /// <summary>
    /// Deletes a topic.
    /// </summary>
    public Task<Res> DeleteTopicAsync(string clusterId, string topicName, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => topicService.DeleteTopicAsync(clusterId, topicName, cancellationToken), "Kafka topic deleted", "Failed to delete Kafka topic");
    }

    /// <summary>
    /// Increases the partition count for a topic.
    /// </summary>
    public Task<Res> IncreasePartitionsAsync(KafkaTopicPartitionRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() =>
        {
            ArgumentNullException.ThrowIfNull(request);
            return topicService.IncreasePartitionsAsync(request, cancellationToken);
        }, "Kafka topic partitions updated", "Failed to update Kafka partitions");
    }

    /// <summary>
    /// Updates topic retention.
    /// </summary>
    public Task<Res> UpdateRetentionAsync(KafkaTopicRetentionRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() =>
        {
            ArgumentNullException.ThrowIfNull(request);
            return topicService.UpdateRetentionAsync(request, cancellationToken);
        }, "Kafka topic retention updated", "Failed to update Kafka retention");
    }

    /// <summary>
    /// Reads a bounded sample of recent messages from a topic.
    /// </summary>
    public Task<Res<KafkaTopicMessageBatch>> ReadTopicMessagesAsync(KafkaTopicMessagesRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() =>
        {
            ArgumentNullException.ThrowIfNull(request);
            return topicService.ReadMessagesAsync(request, cancellationToken);
        }, "Failed to read Kafka topic messages");
    }

    /// <summary>
    /// Gets partition-level retained-message inventory for a topic.
    /// </summary>
    public Task<Res<KafkaTopicBacklogSnapshot>> GetTopicBacklogAsync(
        string clusterId,
        string topicName,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            () => topicService.GetTopicBacklogAsync(clusterId, topicName, cancellationToken),
            "Failed to load Kafka topic backlog");
    }

    /// <summary>
    /// Lists consumer groups.
    /// </summary>
    public Task<Res<IReadOnlyList<KafkaConsumerGroupSummary>>> ListConsumerGroupsAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => consumerGroupService.ListConsumerGroupsAsync(clusterId, cancellationToken), "Failed to load Kafka consumer groups");
    }

    /// <summary>
    /// Gets recent performance snapshots.
    /// </summary>
    public Task<Res<IReadOnlyList<KafkaPerformanceSnapshot>>> GetPerformanceHistoryAsync(
        string clusterId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => performanceService.GetHistoryAsync(clusterId, limit, cancellationToken), "Failed to load Kafka performance history");
    }

    /// <summary>
    /// Captures a fresh performance snapshot.
    /// </summary>
    public Task<Res<KafkaPerformanceSnapshot>> CapturePerformanceAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => performanceService.CaptureAsync(clusterId, cancellationToken), "Failed to capture Kafka performance snapshot");
    }

    private static async Task<Res<T>> ExecuteAsync<T>(Func<Task<T>> action, string failurePrefix)
    {
        try
        {
            return Res.Ok(await action());
        }
        catch (Exception ex)
        {
            return Res.Fail($"{failurePrefix}: {ex.GetMessageRecursively()}");
        }
    }

    private static async Task<Res> ExecuteAsync(Func<Task> action, string successMessage, string failurePrefix)
    {
        try
        {
            await action();
            return Res.Ok(successMessage);
        }
        catch (Exception ex)
        {
            return Res.Fail($"{failurePrefix}: {ex.GetMessageRecursively()}");
        }
    }
}
