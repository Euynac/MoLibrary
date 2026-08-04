using AwesomeAssertions;
using Monica.EventBus.Kafka.Models;
using Monica.EventBus.Kafka.Providers.EfCore;
using Xunit;

namespace Test.Monica.EventBus.Kafka.Providers.EfCore;

public class KafkaPerformanceSnapshotEntityTests
{
    [Fact]
    public void ToModel_WhenConsumerGroupMetricsWereSerialized_ShouldRestoreNestedMemberMetrics()
    {
        var snapshot = new KafkaPerformanceSnapshot
        {
            ClusterId = "cluster-a",
            CapturedAt = new DateTimeOffset(2026, 7, 31, 8, 0, 0, TimeSpan.Zero),
            TotalRetainedMessageCount = 120,
            ConsumerGroupMetrics =
            [
                new KafkaConsumerGroupTopicMetrics
                {
                    TopicName = "orders",
                    GroupId = "billing",
                    State = "Stable",
                    TotalCurrentOffset = 80,
                    TotalLogEndOffset = 120,
                    TotalRetainedMessageCount = 120,
                    TotalLag = 40,
                    ConsumeRatePerSecond = 3.5,
                    WriteRatePerSecond = 4.5,
                    Members =
                    [
                        new KafkaConsumerMemberMetrics
                        {
                            ConsumerId = "consumer-a",
                            Host = "/10.0.0.12",
                            ClientId = "billing-worker",
                            ConsumeRatePerSecond = 3.5,
                            Partitions =
                            [
                                new KafkaConsumerPartitionMetrics
                                {
                                    Partition = 2,
                                    CurrentOffset = 80,
                                    LogEndOffset = 120,
                                    Lag = 40,
                                    ConsumeRatePerSecond = 3.5,
                                    WriteRatePerSecond = 4.5
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var restored = KafkaPerformanceSnapshotEntity.FromModel(snapshot).ToModel();

        restored.TotalRetainedMessageCount.Should().Be(120);
        restored.ConsumerGroupMetrics.Should().BeEquivalentTo(snapshot.ConsumerGroupMetrics);
    }
}
