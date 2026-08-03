using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Services;

/// <summary>
/// Coordinates Kafka consumer group inspection use cases.
/// </summary>
public sealed class KafkaConsumerGroupService(
    KafkaClusterService clusterService,
    IKafkaAdminProvider adminProvider,
    KafkaConsumerMetricsService metricsService)
{
    public async Task<IReadOnlyList<KafkaConsumerGroupSummary>> ListConsumerGroupsAsync(
        string clusterId,
        CancellationToken cancellationToken = default)
    {
        var cluster = await clusterService.GetRequiredDirectAdminClusterAsync(clusterId, cancellationToken);
        return await adminProvider.ListConsumerGroupsAsync(cluster, cancellationToken);
    }

    /// <summary>
    /// Lists consumer groups that currently own assignments or committed offsets for a topic.
    /// </summary>
    /// <param name="clusterId">Target cluster identifier.</param>
    /// <param name="topicName">Target topic name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Consumer groups associated with the topic.</returns>
    public async Task<IReadOnlyList<KafkaConsumerGroupSummary>> ListTopicConsumerGroupsAsync(
        string clusterId,
        string topicName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        var snapshot = await metricsService.CaptureAsync(clusterId, topicName, cancellationToken: cancellationToken);
        return snapshot.GroupMetrics
            .Select(metric => new KafkaConsumerGroupSummary
            {
                GroupId = metric.GroupId,
                TopicName = metric.TopicName,
                State = metric.State,
                MemberCount = metric.Members.Count(member => !string.IsNullOrWhiteSpace(member.ConsumerId)),
                TotalLag = metric.TotalLag
            })
            .OrderBy(group => group.GroupId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Gets the currently registered members and assignments for one consumer group.
    /// </summary>
    /// <param name="clusterId">Target cluster identifier.</param>
    /// <param name="groupId">Consumer group identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current consumer-group members and their assignments.</returns>
    public async Task<IReadOnlyList<KafkaConsumerGroupMemberAssignment>> GetConsumerGroupMembersAsync(
        string clusterId,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        var cluster = await clusterService.GetRequiredDirectAdminClusterAsync(clusterId, cancellationToken);
        var description = await adminProvider.DescribeConsumerGroupAsync(cluster, groupId, cancellationToken);
        return description.Members;
    }

    /// <summary>
    /// Captures member and partition metrics for an optional topic and consumer-group filter.
    /// </summary>
    /// <param name="clusterId">Target cluster identifier.</param>
    /// <param name="topicName">Optional topic filter.</param>
    /// <param name="groupId">Optional consumer-group filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A detailed consumer metrics snapshot.</returns>
    public Task<KafkaConsumerMetricsSnapshot> CaptureMetricsAsync(
        string clusterId,
        string? topicName,
        string? groupId,
        CancellationToken cancellationToken = default)
    {
        return metricsService.CaptureAsync(clusterId, topicName, groupId, cancellationToken);
    }
}
