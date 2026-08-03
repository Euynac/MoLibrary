using System.Buffers.Binary;
using System.Text;
using AwesomeAssertions;
using Monica.EventBus.Kafka.Providers.ConfluentKafka;
using Xunit;

namespace Test.Monica.EventBus.Kafka.Providers.ConfluentKafka;

public class KafkaConsumerProtocolAssignmentDecoderTests
{
    [Fact]
    public void Decode_WhenAssignmentContainsMultipleTopics_ShouldReturnSortedPartitions()
    {
        var payload = BuildAssignment(
            ("payments", [3, 1]),
            ("orders", [2, 0]));

        var result = KafkaConsumerProtocolAssignmentDecoder.Decode(payload);

        result.Should().BeEquivalentTo(
            [
                new { TopicName = "orders", Partition = 0 },
                new { TopicName = "orders", Partition = 2 },
                new { TopicName = "payments", Partition = 1 },
                new { TopicName = "payments", Partition = 3 }
            ],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Decode_WhenPayloadIsEmpty_ShouldReturnNoPartitions()
    {
        var result = KafkaConsumerProtocolAssignmentDecoder.Decode([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Decode_WhenPayloadIsNull_ShouldReturnNoPartitions()
    {
        var result = KafkaConsumerProtocolAssignmentDecoder.Decode(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Decode_WhenUserDataIsPresent_ShouldIgnoreUserData()
    {
        var payload = BuildAssignmentWithUserData(
            [1, 2, 3],
            ("orders", [0]));

        var result = KafkaConsumerProtocolAssignmentDecoder.Decode(payload);

        result.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { TopicName = "orders", Partition = 0 });
    }

    [Fact]
    public void Decode_WhenUserDataIsTruncated_ShouldThrowInvalidDataException()
    {
        var completePayload = BuildAssignmentWithUserData(
            [1, 2],
            ("orders", [0]));
        var payload = completePayload[..^1];

        var act = () => KafkaConsumerProtocolAssignmentDecoder.Decode(payload);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*truncated while reading user data*");
    }

    [Fact]
    public void Decode_WhenTrailingBytesArePresent_ShouldThrowInvalidDataException()
    {
        var payload = BuildAssignment(("orders", [0]))
            .Append((byte)1)
            .ToArray();

        var act = () => KafkaConsumerProtocolAssignmentDecoder.Decode(payload);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*unexpected trailing bytes*");
    }

    [Fact]
    public void Decode_WhenTopicNameIsTruncated_ShouldThrowInvalidDataException()
    {
        var payload = new byte[]
        {
            0, 0,
            0, 0, 0, 1,
            0, 5,
            (byte)'a', (byte)'b', (byte)'c', (byte)'d'
        };

        var act = () => KafkaConsumerProtocolAssignmentDecoder.Decode(payload);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*topic 0 name*");
    }

    [Fact]
    public void Decode_WhenTopicCountIsNegative_ShouldThrowInvalidDataException()
    {
        var payload = new byte[]
        {
            0, 0,
            255, 255, 255, 255
        };

        var act = () => KafkaConsumerProtocolAssignmentDecoder.Decode(payload);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*negative topic count*");
    }

    [Fact]
    public void Decode_WhenPartitionCountExceedsPayload_ShouldThrowInvalidDataException()
    {
        var payload = new byte[]
        {
            0, 0,
            0, 0, 0, 1,
            0, 1, (byte)'a',
            0, 0, 0, 2,
            0, 0, 0, 0
        };

        var act = () => KafkaConsumerProtocolAssignmentDecoder.Decode(payload);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*partition count*remaining payload*");
    }

    [Fact]
    public void Decode_WhenTopicNameContainsInvalidUtf8_ShouldThrowInvalidDataException()
    {
        var payload = new byte[]
        {
            0, 0,
            0, 0, 0, 1,
            0, 2, 0xC3, 0x28,
            0, 0, 0, 0,
            255, 255, 255, 255
        };

        var act = () => KafkaConsumerProtocolAssignmentDecoder.Decode(payload);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*invalid UTF-8*");
    }

    [Fact]
    public void Decode_WhenVersionIsUnsupported_ShouldThrowInvalidDataException()
    {
        var payload = new byte[] { 0, 1 };

        var act = () => KafkaConsumerProtocolAssignmentDecoder.Decode(payload);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*version 1 is not supported*");
    }

    [Fact]
    public void Decode_WhenPartitionIsNegative_ShouldThrowInvalidDataException()
    {
        var payload = BuildAssignment(("orders", [-1]));

        var act = () => KafkaConsumerProtocolAssignmentDecoder.Decode(payload);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*negative partition -1*");
    }

    private static byte[] BuildAssignment(params (string TopicName, int[] Partitions)[] topics)
    {
        return BuildAssignmentWithUserData(null, topics);
    }

    private static byte[] BuildAssignmentWithUserData(
        byte[]? userData,
        params (string TopicName, int[] Partitions)[] topics)
    {
        var payload = new List<byte>();
        WriteInt16(payload, 0);
        WriteInt32(payload, topics.Length);
        foreach (var topic in topics)
        {
            var topicBytes = Encoding.UTF8.GetBytes(topic.TopicName);
            WriteInt16(payload, checked((short)topicBytes.Length));
            payload.AddRange(topicBytes);
            WriteInt32(payload, topic.Partitions.Length);
            foreach (var partition in topic.Partitions)
            {
                WriteInt32(payload, partition);
            }
        }

        WriteInt32(payload, userData?.Length ?? -1);
        if (userData is not null)
        {
            payload.AddRange(userData);
        }

        return [.. payload];
    }

    private static void WriteInt16(List<byte> destination, short value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        destination.AddRange(buffer.ToArray());
    }

    private static void WriteInt32(List<byte> destination, int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        destination.AddRange(buffer.ToArray());
    }
}
