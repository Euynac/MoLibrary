using AwesomeAssertions;
using Monica.EventBus.Kafka.Models;
using Monica.EventBus.Kafka.Services.Support;
using Xunit;

namespace Test.Monica.EventBus.Kafka.Services.Support;

public class KafkaPerformanceRateCalculatorTests
{
    [Fact]
    public void ApplyRates_WhenPerformanceOffsetsAdvance_ShouldCalculateEveryAggregationLevel()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);
        var previous = new KafkaPerformanceSnapshot
        {
            CapturedAt = capturedAt,
            TotalLogEndOffset = 1_000,
            TotalConsumerCommittedOffset = 300,
            TopicMetrics =
            [
                new KafkaTopicPerformanceSnapshot
                {
                    TopicName = "orders",
                    TotalLogEndOffset = 400,
                    TotalConsumerCommittedOffset = 120
                }
            ],
            ConsumerGroupMetrics =
            [
                CreateGroupMetric("orders", "billing", capturedAt, 100, 50, "member-old")
            ]
        };
        var current = new KafkaPerformanceSnapshot
        {
            CapturedAt = capturedAt.AddSeconds(10),
            TotalLogEndOffset = 1_050,
            TotalConsumerCommittedOffset = 330,
            TopicMetrics =
            [
                new KafkaTopicPerformanceSnapshot
                {
                    TopicName = "orders",
                    TotalLogEndOffset = 430,
                    TotalConsumerCommittedOffset = 140
                }
            ],
            ConsumerGroupMetrics =
            [
                CreateGroupMetric("orders", "billing", capturedAt.AddSeconds(10), 140, 70, "member-new")
            ]
        };

        KafkaPerformanceRateCalculator.ApplyRates(current, previous);

        current.MessageWriteRatePerSecond.Should().Be(5d);
        current.MessageConsumeRatePerSecond.Should().Be(3d);
        current.TopicMetrics.Single().MessageWriteRatePerSecond.Should().Be(3d);
        current.TopicMetrics.Single().MessageConsumeRatePerSecond.Should().Be(2d);
        current.ConsumerGroupMetrics.Single().WriteRatePerSecond.Should().Be(4d);
        current.ConsumerGroupMetrics.Single().ConsumeRatePerSecond.Should().Be(2d);
        var member = current.ConsumerGroupMetrics.Single().Members.Single();
        member.ConsumeRatePerSecond.Should().Be(2d);
        var partition = member.Partitions.Single();
        partition.WriteRatePerSecond.Should().Be(4d);
        partition.ConsumeRatePerSecond.Should().Be(2d);
    }

    [Fact]
    public void ApplyRates_WhenPartitionMovesToAnotherMember_ShouldKeepTopicAndGroupPartitionBaseline()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);
        var previous = new KafkaConsumerMetricsSnapshot
        {
            CapturedAt = capturedAt,
            GroupMetrics =
            [
                CreateGroupMetric("orders", "billing", capturedAt, 40, 20, "member-old"),
                CreateGroupMetric("orders", "shipping", capturedAt, 200, 100, "member-other")
            ]
        };
        var current = new KafkaConsumerMetricsSnapshot
        {
            CapturedAt = capturedAt.AddSeconds(6),
            GroupMetrics =
            [
                CreateGroupMetric("orders", "billing", capturedAt.AddSeconds(6), 70, 32, "member-new")
            ]
        };

        KafkaPerformanceRateCalculator.ApplyRates(current, previous);

        var metric = current.GroupMetrics.Single();
        metric.WriteRatePerSecond.Should().Be(5d);
        metric.ConsumeRatePerSecond.Should().Be(2d);
        var member = metric.Members.Single();
        member.ConsumeRatePerSecond.Should().Be(2d);
        var partition = member.Partitions.Single();
        partition.WriteRatePerSecond.Should().Be(5d);
        partition.ConsumeRatePerSecond.Should().Be(2d);
    }

    [Fact]
    public void ApplyRates_WhenOffsetsMoveBackward_ShouldLeaveRatesUnknown()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);
        var previous = new KafkaConsumerMetricsSnapshot
        {
            CapturedAt = capturedAt,
            GroupMetrics =
            [
                CreateGroupMetric("orders", "billing", capturedAt, 100, 50, "member-a")
            ]
        };
        var current = new KafkaConsumerMetricsSnapshot
        {
            CapturedAt = capturedAt.AddSeconds(5),
            GroupMetrics =
            [
                CreateGroupMetric("orders", "billing", capturedAt.AddSeconds(5), 90, 45, "member-a")
            ]
        };

        KafkaPerformanceRateCalculator.ApplyRates(current, previous);

        var metric = current.GroupMetrics.Single();
        metric.WriteRatePerSecond.Should().BeNull();
        metric.ConsumeRatePerSecond.Should().BeNull();
        var member = metric.Members.Single();
        member.ConsumeRatePerSecond.Should().BeNull();
        var partition = member.Partitions.Single();
        partition.WriteRatePerSecond.Should().BeNull();
        partition.ConsumeRatePerSecond.Should().BeNull();
    }

    [Fact]
    public void ApplyRates_WhenOneMemberOffsetIsUnknown_ShouldKeepOtherMemberRateAvailable()
    {
        var capturedAt = new DateTimeOffset(2026, 8, 3, 1, 0, 0, TimeSpan.Zero);
        var previous = new KafkaConsumerMetricsSnapshot
        {
            CapturedAt = capturedAt,
            GroupMetrics =
            [
                CreateGroupMetricWithMembers(
                    capturedAt,
                    ("member-unknown", 0, null),
                    ("member-active", 1, 100))
            ]
        };
        var current = new KafkaConsumerMetricsSnapshot
        {
            CapturedAt = capturedAt.AddSeconds(10),
            GroupMetrics =
            [
                CreateGroupMetricWithMembers(
                    capturedAt.AddSeconds(10),
                    ("member-unknown", 0, null),
                    ("member-active", 1, 125))
            ]
        };

        KafkaPerformanceRateCalculator.ApplyRates(current, previous);

        var members = current.GroupMetrics.Single().Members.ToDictionary(member => member.ConsumerId);
        members["member-unknown"].ConsumeRatePerSecond.Should().BeNull();
        members["member-active"].ConsumeRatePerSecond.Should().Be(2.5d);
        members["member-active"].Partitions.Single().ConsumeRatePerSecond.Should().Be(2.5d);
        current.GroupMetrics.Single().ConsumeRatePerSecond.Should().BeNull();
    }

    private static KafkaConsumerGroupTopicMetrics CreateGroupMetric(
        string topicName,
        string groupId,
        DateTimeOffset capturedAt,
        long logEndOffset,
        long currentOffset,
        string consumerId)
    {
        return new KafkaConsumerGroupTopicMetrics
        {
            TopicName = topicName,
            GroupId = groupId,
            CapturedAt = capturedAt,
            TotalLogEndOffset = logEndOffset,
            TotalCurrentOffset = currentOffset,
            Members =
            [
                new KafkaConsumerMemberMetrics
                {
                    ConsumerId = consumerId,
                    Partitions =
                    [
                        new KafkaConsumerPartitionMetrics
                        {
                            Partition = 0,
                            LogEndOffset = logEndOffset,
                            CurrentOffset = currentOffset
                        }
                    ]
                }
            ]
        };
    }

    private static KafkaConsumerGroupTopicMetrics CreateGroupMetricWithMembers(
        DateTimeOffset capturedAt,
        params (string ConsumerId, int Partition, long? CurrentOffset)[] members)
    {
        return new KafkaConsumerGroupTopicMetrics
        {
            TopicName = "orders",
            GroupId = "billing",
            CapturedAt = capturedAt,
            TotalCurrentOffset = null,
            Members = members
                .Select(member => new KafkaConsumerMemberMetrics
                {
                    ConsumerId = member.ConsumerId,
                    Partitions =
                    [
                        new KafkaConsumerPartitionMetrics
                        {
                            Partition = member.Partition,
                            CurrentOffset = member.CurrentOffset,
                            LogEndOffset = 200 + member.Partition
                        }
                    ]
                })
                .ToList()
        };
    }
}
