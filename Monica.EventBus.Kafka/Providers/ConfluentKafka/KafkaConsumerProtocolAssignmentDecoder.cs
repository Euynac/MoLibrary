using System.Buffers.Binary;
using System.Text;
using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Providers.ConfluentKafka;

/// <summary>
/// Decodes the classic Kafka consumer protocol member-assignment payload returned by ListGroups.
/// </summary>
/// <remarks>
/// Assignment versions 0 through 3 share the same topic-partition and user-data layout. Later
/// versions remain rejected until their wire format is explicitly verified.
/// </remarks>
internal static class KafkaConsumerProtocolAssignmentDecoder
{
    private const short MIN_SUPPORTED_VERSION = 0;
    private const short MAX_SUPPORTED_VERSION = 3;
    private const int MAX_TOPIC_COUNT = 100_000;
    private const int MAX_PARTITION_ASSIGNMENT_COUNT = 1_000_000;
    private static readonly UTF8Encoding STRICT_UTF8 = new(false, true);

    /// <summary>
    /// Decodes topic-partition assignments while rejecting malformed or unexpectedly large payloads.
    /// </summary>
    public static IReadOnlyList<KafkaConsumerPartitionAssignment> Decode(byte[]? payload)
    {
        if (payload is null || payload.Length == 0)
        {
            return [];
        }

        var reader = new AssignmentReader(payload);
        var version = reader.ReadInt16("assignment version");
        if (version is < MIN_SUPPORTED_VERSION or > MAX_SUPPORTED_VERSION)
        {
            throw new InvalidDataException($"Kafka consumer assignment version {version} is not supported.");
        }

        var topicCount = reader.ReadNonNegativeInt32("topic count");
        reader.ValidateCollectionCount(topicCount, MAX_TOPIC_COUNT, sizeof(short) + sizeof(int), "topic count");

        var assignments = new List<KafkaConsumerPartitionAssignment>();
        for (var topicIndex = 0; topicIndex < topicCount; topicIndex++)
        {
            var topicName = reader.ReadString($"topic {topicIndex} name");
            var partitionCount = reader.ReadNonNegativeInt32($"topic {topicIndex} partition count");
            reader.ValidateCollectionCount(
                partitionCount,
                MAX_PARTITION_ASSIGNMENT_COUNT,
                sizeof(int),
                $"topic {topicIndex} partition count");
            if (assignments.Count > MAX_PARTITION_ASSIGNMENT_COUNT - partitionCount)
            {
                throw new InvalidDataException(
                    $"Kafka consumer assignment exceeds the {MAX_PARTITION_ASSIGNMENT_COUNT} partition safety limit.");
            }

            for (var partitionIndex = 0; partitionIndex < partitionCount; partitionIndex++)
            {
                var partition = reader.ReadInt32($"topic {topicIndex} partition {partitionIndex}");
                if (partition < 0)
                {
                    throw new InvalidDataException(
                        $"Kafka consumer assignment contains negative partition {partition} for topic '{topicName}'.");
                }

                assignments.Add(new KafkaConsumerPartitionAssignment
                {
                    TopicName = topicName,
                    Partition = partition
                });
            }
        }

        reader.SkipNullableBytes("user data");
        reader.EnsureFullyConsumed();

        return assignments
            .OrderBy(assignment => assignment.TopicName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(assignment => assignment.Partition)
            .ToList();
    }

    private ref struct AssignmentReader(ReadOnlySpan<byte> payload)
    {
        private readonly ReadOnlySpan<byte> _payload = payload;
        private int _offset;

        public int Remaining => _payload.Length - _offset;

        public short ReadInt16(string fieldName)
        {
            return BinaryPrimitives.ReadInt16BigEndian(Read(fieldName, sizeof(short)));
        }

        public int ReadInt32(string fieldName)
        {
            return BinaryPrimitives.ReadInt32BigEndian(Read(fieldName, sizeof(int)));
        }

        public int ReadNonNegativeInt32(string fieldName)
        {
            var value = ReadInt32(fieldName);
            if (value < 0)
            {
                throw new InvalidDataException($"Kafka consumer assignment contains negative {fieldName} {value}.");
            }

            return value;
        }

        public string ReadString(string fieldName)
        {
            var byteLength = ReadInt16($"{fieldName} length");
            if (byteLength < 0)
            {
                throw new InvalidDataException(
                    $"Kafka consumer assignment contains negative {fieldName} length {byteLength}.");
            }

            var value = Read(fieldName, byteLength);
            try
            {
                return STRICT_UTF8.GetString(value);
            }
            catch (DecoderFallbackException ex)
            {
                throw new InvalidDataException(
                    $"Kafka consumer assignment contains invalid UTF-8 in {fieldName}.",
                    ex);
            }
        }

        public void ValidateCollectionCount(
            int count,
            int maximumCount,
            int minimumBytesPerItem,
            string fieldName)
        {
            if (count > maximumCount)
            {
                throw new InvalidDataException(
                    $"Kafka consumer assignment {fieldName} {count} exceeds the {maximumCount} safety limit.");
            }

            if (count > Remaining / minimumBytesPerItem)
            {
                throw new InvalidDataException(
                    $"Kafka consumer assignment {fieldName} {count} exceeds the remaining payload.");
            }
        }

        public void Skip(int byteLength, string fieldName)
        {
            _ = Read(fieldName, byteLength);
        }

        public void SkipNullableBytes(string fieldName)
        {
            var byteLength = ReadInt32($"{fieldName} length");
            if (byteLength < -1)
            {
                throw new InvalidDataException(
                    $"Kafka consumer assignment contains invalid {fieldName} length {byteLength}.");
            }

            if (byteLength >= 0)
            {
                Skip(byteLength, fieldName);
            }
        }

        public void EnsureFullyConsumed()
        {
            if (Remaining != 0)
            {
                throw new InvalidDataException(
                    $"Kafka consumer assignment contains {Remaining} unexpected trailing bytes.");
            }
        }

        private ReadOnlySpan<byte> Read(string fieldName, int byteLength)
        {
            if (byteLength < 0 || byteLength > Remaining)
            {
                throw new InvalidDataException(
                    $"Kafka consumer assignment is truncated while reading {fieldName}.");
            }

            var value = _payload.Slice(_offset, byteLength);
            _offset += byteLength;
            return value;
        }
    }
}
