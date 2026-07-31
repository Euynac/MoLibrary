using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;
using Monica.EventBus.Kafka.Services.Support;

namespace Monica.EventBus.Kafka.Services;

/// <summary>
/// Coordinates read-only consumer-group assignment and offset metric captures.
/// </summary>
/// <remarks>
/// The service keeps the previous UI-scoped capture for interval-rate calculation. Persisted
/// performance snapshots supply their own baseline so performance sampling remains stable across
/// requests and repository implementations.
/// </remarks>
public sealed class KafkaConsumerMetricsService(
    KafkaClusterService clusterService,
    IKafkaAdminProvider adminProvider,
    IKafkaOffsetMetricsProvider offsetMetricsProvider)
{
    private readonly Dictionary<string, KafkaConsumerMetricsSnapshot> _previousCaptures =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _captureGate = new(1, 1);

    /// <summary>
    /// Captures consumer metrics for an optional topic and consumer-group filter.
    /// </summary>
    /// <param name="clusterId">Target cluster identifier.</param>
    /// <param name="topicName">Optional topic filter.</param>
    /// <param name="groupId">Optional consumer-group filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A fresh detailed metrics snapshot.</returns>
    public async Task<KafkaConsumerMetricsSnapshot> CaptureAsync(
        string clusterId,
        string? topicName = null,
        string? groupId = null,
        CancellationToken cancellationToken = default)
    {
        await _captureGate.WaitAsync(cancellationToken);
        try
        {
            var cluster = await clusterService.GetRequiredDirectAdminClusterAsync(clusterId, cancellationToken);
            var topics = await adminProvider.ListTopicMetadataAsync(cluster, cancellationToken);
            var groups = await adminProvider.ListConsumerGroupsAsync(cluster, cancellationToken);
            var filteredTopics = FilterTopics(topics, topicName);
            var filteredGroups = FilterGroups(groups, groupId);
            var captureKey = BuildCaptureKey(cluster.ClusterId, topicName, groupId);
            _previousCaptures.TryGetValue(captureKey, out var previous);

            var current = await CaptureCoreAsync(
                cluster,
                filteredTopics,
                filteredGroups,
                failWhenRequestedGroupFails: !string.IsNullOrWhiteSpace(groupId),
                cancellationToken);
            KafkaPerformanceRateCalculator.ApplyRates(current, previous);
            _previousCaptures[captureKey] = current;
            return current;
        }
        finally
        {
            _captureGate.Release();
        }
    }

    /// <summary>
    /// Captures detailed metrics as part of a cluster performance sampling cycle.
    /// </summary>
    /// <param name="cluster">Target cluster configuration.</param>
    /// <param name="topics">Topics already loaded for the performance cycle.</param>
    /// <param name="groups">Consumer groups already loaded for the performance cycle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed metrics aligned with the performance capture.</returns>
    public async Task<KafkaConsumerMetricsSnapshot> CaptureForPerformanceAsync(
        KafkaClusterConfig cluster,
        IReadOnlyList<KafkaTopicSummary> topics,
        IReadOnlyList<KafkaConsumerGroupSummary> groups,
        CancellationToken cancellationToken = default)
    {
        await _captureGate.WaitAsync(cancellationToken);
        try
        {
            return await CaptureCoreAsync(
                cluster,
                topics,
                groups,
                failWhenRequestedGroupFails: false,
                cancellationToken);
        }
        finally
        {
            _captureGate.Release();
        }
    }

    private async Task<KafkaConsumerMetricsSnapshot> CaptureCoreAsync(
        KafkaClusterConfig cluster,
        IReadOnlyList<KafkaTopicSummary> topics,
        IReadOnlyList<KafkaConsumerGroupSummary> groups,
        bool failWhenRequestedGroupFails,
        CancellationToken cancellationToken)
    {
        var groupIds = groups
            .Select(group => group.GroupId)
            .Where(groupId => !string.IsNullOrWhiteSpace(groupId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        IReadOnlyList<KafkaConsumerGroupDescription> descriptions;
        var diagnostics = new List<string>();
        try
        {
            descriptions = await adminProvider.DescribeConsumerGroupsAsync(
                cluster,
                groupIds,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (!failWhenRequestedGroupFails)
        {
            diagnostics.Add(ex.Message);
            descriptions = groups.Select(group => new KafkaConsumerGroupDescription
            {
                GroupId = group.GroupId,
                State = group.State,
                ErrorMessage = ex.Message
            }).ToList();
        }

        ThrowIfRequestedGroupCaptureFailed(descriptions, failWhenRequestedGroupFails);

        var snapshot = await offsetMetricsProvider.CaptureConsumerMetricsAsync(
            cluster,
            topics,
            descriptions,
            cancellationToken);
        snapshot.ConsumerGroups = descriptions;
        ThrowIfRequestedGroupCaptureFailed(descriptions, failWhenRequestedGroupFails);

        diagnostics.AddRange(descriptions
            .Where(description => !string.IsNullOrWhiteSpace(description.ErrorMessage))
            .Select(description => $"{description.GroupId}: {description.ErrorMessage}"));
        if (!string.IsNullOrWhiteSpace(snapshot.Message))
        {
            diagnostics.Add(snapshot.Message);
        }

        var allDiagnostics = diagnostics
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (allDiagnostics.Count > 0)
        {
            snapshot.Message = string.Join("; ", allDiagnostics);
        }

        return snapshot;
    }

    private static void ThrowIfRequestedGroupCaptureFailed(
        IReadOnlyList<KafkaConsumerGroupDescription> descriptions,
        bool failWhenRequestedGroupFails)
    {
        if (!failWhenRequestedGroupFails)
        {
            return;
        }

        var failed = descriptions.FirstOrDefault(description =>
            !string.IsNullOrWhiteSpace(description.ErrorMessage));
        if (failed is not null)
        {
            throw new InvalidOperationException(
                $"Kafka consumer group '{failed.GroupId}' metrics could not be captured: {failed.ErrorMessage}");
        }
    }

    private static IReadOnlyList<KafkaTopicSummary> FilterTopics(
        IReadOnlyList<KafkaTopicSummary> topics,
        string? topicName)
    {
        if (string.IsNullOrWhiteSpace(topicName))
        {
            return topics;
        }

        var normalizedTopicName = topicName.Trim();
        var matches = topics
            .Where(topic => string.Equals(topic.TopicName, normalizedTopicName, StringComparison.Ordinal))
            .ToList();
        if (matches.Count == 0)
        {
            throw new KeyNotFoundException($"Kafka topic '{normalizedTopicName}' was not found.");
        }

        return matches;
    }

    private static IReadOnlyList<KafkaConsumerGroupSummary> FilterGroups(
        IReadOnlyList<KafkaConsumerGroupSummary> groups,
        string? groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return groups;
        }

        var normalizedGroupId = groupId.Trim();
        var matches = groups
            .Where(group => string.Equals(group.GroupId, normalizedGroupId, StringComparison.Ordinal))
            .ToList();
        if (matches.Count == 0)
        {
            throw new KeyNotFoundException($"Kafka consumer group '{normalizedGroupId}' was not found.");
        }

        return matches;
    }

    private static string BuildCaptureKey(string clusterId, string? topicName, string? groupId)
    {
        return string.Join('\u001f', clusterId, topicName?.Trim() ?? string.Empty, groupId?.Trim() ?? string.Empty);
    }
}
