using Monica.EventBus.Kafka.Models;
using MudBlazor;

namespace Monica.EventBus.Kafka.UIEventBusKafka.Support;

/// <summary>
/// Projects a consumer metrics snapshot into the member and partition rows required by the detail dialog.
/// </summary>
internal sealed class KafkaConsumerGroupMetricsView
{
    private KafkaConsumerGroupMetricsView(
        IReadOnlyList<KafkaConsumerMemberMetricsView> members,
        IReadOnlyList<KafkaConsumerPartitionMetricsRow> partitionRows)
    {
        Members = members;
        AssignmentCount = members.Sum(member => member.AssignmentCount);
        TotalLag = SumKnown(members.Select(member => member.TotalLag));
        ConsumeRatePerSecond = SumKnown(members.Select(member => member.ConsumeRatePerSecond));
        PartitionRows = partitionRows;
    }

    /// <summary>
    /// Gets the live members and their independently aggregated metrics.
    /// </summary>
    public IReadOnlyList<KafkaConsumerMemberMetricsView> Members { get; }

    /// <summary>
    /// Gets the number of live members in the selected group and topic scope.
    /// </summary>
    public int MemberCount => Members.Count;

    /// <summary>
    /// Gets whether the selected scope contains live members.
    /// </summary>
    public bool HasMembers => MemberCount > 0;

    /// <summary>
    /// Gets the total number of topic-partition assignments owned by live members in the selected scope.
    /// </summary>
    public int AssignmentCount { get; }

    /// <summary>
    /// Gets the aggregate lag for the selected scope, or null when any member lag is unknown.
    /// </summary>
    public long? TotalLag { get; }

    /// <summary>
    /// Gets the aggregate consume rate for the selected scope, or null when any member rate is unknown.
    /// </summary>
    public double? ConsumeRatePerSecond { get; }

    /// <summary>
    /// Gets the semantic color used by the group member-count summary.
    /// </summary>
    public Color MemberCountAccentColor => HasMembers ? Color.Secondary : Color.Warning;

    /// <summary>
    /// Gets the semantic color used by the group assignment-count summary.
    /// </summary>
    public Color AssignmentCountAccentColor => AssignmentCount > 0 ? Color.Info : Color.Warning;

    /// <summary>
    /// Gets the semantic color used by the group lag summary.
    /// </summary>
    public Color LagAccentColor => KafkaConsumerPartitionMetricsRow.GetLagColor(TotalLag);

    /// <summary>
    /// Gets the semantic color used by the group consume-rate summary.
    /// </summary>
    public Color ConsumeRateAccentColor => ConsumeRatePerSecond.HasValue ? Color.Success : Color.Warning;

    /// <summary>
    /// Gets the partition rows in topic and partition order.
    /// </summary>
    public IReadOnlyList<KafkaConsumerPartitionMetricsRow> PartitionRows { get; }

    /// <summary>
    /// Gets the number of partition metric rows in the selected scope.
    /// </summary>
    public int PartitionCount => PartitionRows.Count;

    /// <summary>
    /// Gets whether the selected scope contains partition metrics.
    /// </summary>
    public bool HasPartitions => PartitionCount > 0;

    /// <summary>
    /// Creates a view for one consumer group and an optional topic filter.
    /// </summary>
    public static KafkaConsumerGroupMetricsView Create(
        KafkaConsumerMetricsSnapshot? snapshot,
        string groupId,
        string? topicName)
    {
        var topicFilter = string.IsNullOrWhiteSpace(topicName) ? null : topicName;
        var groupMetrics = (snapshot?.GroupMetrics ?? [])
            .Where(metric => string.Equals(metric.GroupId, groupId, StringComparison.Ordinal) &&
                             (topicFilter is null ||
                              string.Equals(metric.TopicName, topicFilter, StringComparison.Ordinal)))
            .ToList();
        IReadOnlyList<KafkaConsumerGroupMemberAssignment> memberAssignments = snapshot?.ConsumerGroups
            .FirstOrDefault(group => string.Equals(group.GroupId, groupId, StringComparison.Ordinal))
            ?.Members ?? [];

        if (topicFilter is not null)
        {
            memberAssignments = memberAssignments
                .Where(member => member.Partitions.Any(partition =>
                    string.Equals(partition.TopicName, topicFilter, StringComparison.Ordinal)))
                .ToList();
        }

        var metricsByMemberId = groupMetrics
            .SelectMany(metric => metric.Members)
            .Where(member => !string.IsNullOrWhiteSpace(member.ConsumerId))
            .GroupBy(member => member.ConsumerId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var members = memberAssignments
            .Select(member => CreateMemberView(member, topicFilter, metricsByMemberId))
            .OrderBy(member => member.Member.ConsumerId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var partitionRows = groupMetrics
            .SelectMany(metric => metric.Members.SelectMany(member => member.Partitions.Select(partition =>
                new KafkaConsumerPartitionMetricsRow(metric.TopicName, member, partition))))
            .OrderBy(row => row.TopicName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Partition.Partition)
            .ToList();

        return new KafkaConsumerGroupMetricsView(members, partitionRows);
    }

    private static KafkaConsumerMemberMetricsView CreateMemberView(
        KafkaConsumerGroupMemberAssignment member,
        string? topicName,
        IReadOnlyDictionary<string, List<KafkaConsumerMemberMetrics>> metricsByMemberId)
    {
        var assignmentCount = topicName is null
            ? member.Partitions.Count
            : member.Partitions.Count(partition =>
                string.Equals(partition.TopicName, topicName, StringComparison.Ordinal));
        var memberTopicMetrics = metricsByMemberId.GetValueOrDefault(member.ConsumerId) ?? [];
        var totalLag = SumKnown(memberTopicMetrics
            .SelectMany(metric => metric.Partitions)
            .Select(partition => partition.Lag));
        var consumeRate = SumKnown(memberTopicMetrics.Select(metric => metric.ConsumeRatePerSecond));

        return new KafkaConsumerMemberMetricsView(member, assignmentCount, totalLag, consumeRate);
    }

    private static long? SumKnown(IEnumerable<long?> values)
    {
        var materialized = values.ToList();
        return materialized.Count == 0 || materialized.Any(value => !value.HasValue)
            ? null
            : materialized.Sum(value => value!.Value);
    }

    private static double? SumKnown(IEnumerable<double?> values)
    {
        var materialized = values.ToList();
        return materialized.Count == 0 || materialized.Any(value => !value.HasValue)
            ? null
            : materialized.Sum(value => value!.Value);
    }
}

/// <summary>
/// Display metrics for one live consumer-group member.
/// </summary>
internal sealed record KafkaConsumerMemberMetricsView(
    KafkaConsumerGroupMemberAssignment Member,
    int AssignmentCount,
    long? TotalLag,
    double? ConsumeRatePerSecond)
{
    /// <summary>
    /// Gets the semantic color used by the member identity card.
    /// </summary>
    public Color MemberAccentColor => Color.Secondary;

    /// <summary>
    /// Gets the semantic color used by the assignment-count card.
    /// </summary>
    public Color AssignmentAccentColor => AssignmentCount > 0 ? Color.Info : Color.Warning;

    /// <summary>
    /// Gets the semantic color used by the lag card.
    /// </summary>
    public Color LagAccentColor => KafkaConsumerPartitionMetricsRow.GetLagColor(TotalLag);

    /// <summary>
    /// Gets the semantic color used by the consume-rate card.
    /// </summary>
    public Color ConsumeRateAccentColor => ConsumeRatePerSecond.HasValue ? Color.Success : Color.Warning;
}

/// <summary>
/// One partition metric row with its owning member and topic context.
/// </summary>
internal sealed record KafkaConsumerPartitionMetricsRow(
    string TopicName,
    KafkaConsumerMemberMetrics Member,
    KafkaConsumerPartitionMetrics Partition)
{
    /// <summary>
    /// Gets the semantic color used by the partition lag chip.
    /// </summary>
    public Color LagColor => GetLagColor(Partition.Lag);

    /// <summary>
    /// Resolves a lag value to its semantic MudBlazor color.
    /// </summary>
    public static Color GetLagColor(long? lag) => lag switch
    {
        0 => Color.Success,
        > 0 => Color.Warning,
        _ => Color.Secondary
    };
}
