using AwesomeAssertions;
using Monica.EventBus.Kafka.Models;
using Monica.EventBus.Kafka.UIEventBusKafka.Support;
using MudBlazor;
using Xunit;

namespace Test.Monica.EventBus.Kafka.UIEventBusKafka.Support;

public class KafkaConsumerGroupMetricsViewTests
{
    [Fact]
    public void Create_WhenOneMemberRateIsUnknown_ShouldKeepOtherMemberMetricsIndependent()
    {
        var snapshot = new KafkaConsumerMetricsSnapshot
        {
            ConsumerGroups =
            [
                new KafkaConsumerGroupDescription
                {
                    GroupId = "billing",
                    Members =
                    [
                        CreateMemberAssignment("member-unknown", "/10.0.0.1", "worker-a", 0),
                        CreateMemberAssignment("member-active", "/10.0.0.2", "worker-b", 1)
                    ]
                }
            ],
            GroupMetrics =
            [
                new KafkaConsumerGroupTopicMetrics
                {
                    TopicName = "orders",
                    GroupId = "billing",
                    Members =
                    [
                        CreateMemberMetrics("member-unknown", 0, null, null),
                        CreateMemberMetrics("member-active", 1, 0, 2.5)
                    ]
                }
            ]
        };

        var view = KafkaConsumerGroupMetricsView.Create(snapshot, "billing", "orders");

        view.MemberCount.Should().Be(2);
        var members = view.Members.ToDictionary(member => member.Member.ConsumerId);
        members["member-unknown"].AssignmentCount.Should().Be(1);
        members["member-unknown"].TotalLag.Should().BeNull();
        members["member-unknown"].ConsumeRatePerSecond.Should().BeNull();
        members["member-unknown"].ConsumeRateAccentColor.Should().Be(Color.Warning);
        members["member-active"].AssignmentCount.Should().Be(1);
        members["member-active"].TotalLag.Should().Be(0);
        members["member-active"].ConsumeRatePerSecond.Should().Be(2.5);
        members["member-active"].ConsumeRateAccentColor.Should().Be(Color.Success);
        view.PartitionRows.Should().HaveCount(2);
    }

    private static KafkaConsumerGroupMemberAssignment CreateMemberAssignment(
        string consumerId,
        string host,
        string clientId,
        int partition)
    {
        return new KafkaConsumerGroupMemberAssignment
        {
            ConsumerId = consumerId,
            Host = host,
            ClientId = clientId,
            Partitions =
            [
                new KafkaConsumerPartitionAssignment
                {
                    TopicName = "orders",
                    Partition = partition
                }
            ]
        };
    }

    private static KafkaConsumerMemberMetrics CreateMemberMetrics(
        string consumerId,
        int partition,
        long? lag,
        double? consumeRate)
    {
        return new KafkaConsumerMemberMetrics
        {
            ConsumerId = consumerId,
            ConsumeRatePerSecond = consumeRate,
            Partitions =
            [
                new KafkaConsumerPartitionMetrics
                {
                    Partition = partition,
                    Lag = lag,
                    ConsumeRatePerSecond = consumeRate
                }
            ]
        };
    }
}
