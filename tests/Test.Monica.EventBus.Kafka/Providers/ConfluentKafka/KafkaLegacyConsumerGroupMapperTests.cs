using Confluent.Kafka;
using AwesomeAssertions;
using Monica.EventBus.Kafka.Providers.ConfluentKafka;
using Xunit;

namespace Test.Monica.EventBus.Kafka.Providers.ConfluentKafka;

public class KafkaLegacyConsumerGroupMapperTests
{
    [Fact]
    public void Map_WhenLegacyGroupContainsAssignment_ShouldPreserveMemberIdentityAndPartition()
    {
        var assignment = new byte[]
        {
            0, 0,
            0, 0, 0, 1,
            0, 6, (byte)'o', (byte)'r', (byte)'d', (byte)'e', (byte)'r', (byte)'s',
            0, 0, 0, 1,
            0, 0, 0, 2,
            255, 255, 255, 255
        };
        var group = CreateGroup(
            new GroupMemberInfo("consumer-a", "client-a", "/10.0.0.12", [], assignment));

        var result = KafkaLegacyConsumerGroupMapper.Map(group);

        result.ErrorMessage.Should().BeNull();
        result.State.Should().Be("Stable");
        var member = result.Members.Should().ContainSingle().Subject;
        member.ConsumerId.Should().Be("consumer-a");
        member.ClientId.Should().Be("client-a");
        member.Host.Should().Be("/10.0.0.12");
        member.Partitions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { TopicName = "orders", Partition = 2 });
    }

    [Fact]
    public void Map_WhenMemberAssignmentIsMalformed_ShouldContainDiagnosticToGroup()
    {
        var group = CreateGroup(
            new GroupMemberInfo("consumer-a", "client-a", "/10.0.0.12", [], [0, 0, 0]));

        var result = KafkaLegacyConsumerGroupMapper.Map(group);

        result.ErrorMessage.Should().Contain("invalid member assignment");
        result.Members.Should().BeEmpty();
    }

    private static GroupInfo CreateGroup(params GroupMemberInfo[] members)
    {
        return new GroupInfo(
            new BrokerMetadata(1, "broker", 9092),
            "billing",
            new Error(ErrorCode.NoError),
            "Stable",
            "consumer",
            "range",
            [.. members]);
    }
}
