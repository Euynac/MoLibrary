using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;
using Monica.EventBus.Kafka.Abstractions;
using Monica.EventBus.Kafka.Models;
using Monica.EventBus.Kafka.Services.Support;
using Monica.Modules;

namespace Monica.EventBus.Kafka.Providers.ConfluentKafka;

/// <summary>
/// Confluent Kafka implementation of read-only message previews.
/// </summary>
internal sealed class ConfluentKafkaMessageReader(IOptions<ModuleEventBusKafkaOption> options) : IKafkaMessageReader
{
    private const string SERVICE_KEY = "message-preview";
    private static readonly UTF8Encoding STRICT_UTF8 = new(false, true);

    private ModuleEventBusKafkaOption Option => options.Value;

    public async Task<KafkaTopicMessageBatch> ReadMessagesAsync(
        KafkaClusterConfig cluster,
        KafkaTopicMessagesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TopicName);
        cancellationToken.ThrowIfCancellationRequested();

        var maxMessages = Math.Clamp(request.MaxMessages, 1, Math.Max(1, Option.MessagePreviewMaxMessages));
        using var admin = CreateAdminClient(cluster);
        var topicName = request.TopicName.Trim();
        var partitions = ResolvePartitions(admin, topicName);
        var watermarks = await ReadWatermarksAsync(admin, partitions, cancellationToken);
        var assignments = BuildAssignments(watermarks, maxMessages);
        IReadOnlyList<KafkaTopicMessageSample> messages = [];
        if (assignments.Count > 0)
        {
            using var consumer = CreateConsumer(cluster);
            messages = ConsumePreview(consumer, assignments, watermarks, maxMessages, cancellationToken);
        }

        return new KafkaTopicMessageBatch
        {
            ClusterId = cluster.ClusterId,
            TopicName = topicName,
            MaxMessages = maxMessages,
            CapturedAt = DateTimeOffset.UtcNow,
            Messages = messages
        };
    }

    private IConsumer<byte[], byte[]> CreateConsumer(KafkaClusterConfig cluster)
    {
        if (!cluster.HasDirectKafkaAccess)
        {
            throw new InvalidOperationException(
                $"Kafka cluster '{cluster.ClusterId}' does not expose direct broker credentials to Monica.");
        }

        return new ConsumerBuilder<byte[], byte[]>(
                KafkaClientConfigFactory.BuildReadOnlyConsumerConfig(cluster, Option, SERVICE_KEY))
            .Build();
    }

    private IAdminClient CreateAdminClient(KafkaClusterConfig cluster)
    {
        if (!cluster.HasDirectKafkaAccess)
        {
            throw new InvalidOperationException(
                $"Kafka cluster '{cluster.ClusterId}' does not expose direct broker credentials to Monica.");
        }

        return new AdminClientBuilder(KafkaClientConfigFactory.BuildAdminConfig(cluster, Option)).Build();
    }

    private IReadOnlyList<TopicPartition> ResolvePartitions(IAdminClient admin, string topicName)
    {
        var metadata = admin.GetMetadata(topicName, Option.AdminRequestTimeout);
        var topic = metadata.Topics.FirstOrDefault(item =>
            string.Equals(item.Topic, topicName, StringComparison.Ordinal));

        if (topic is null || topic.Error.Code != ErrorCode.NoError)
        {
            throw new InvalidOperationException($"Kafka topic '{topicName}' metadata is not available.");
        }

        return topic.Partitions
            .Where(partition => partition.Error.Code == ErrorCode.NoError)
            .Select(partition => new TopicPartition(topicName, new Partition(partition.PartitionId)))
            .OrderBy(partition => partition.Partition.Value)
            .ToList();
    }

    private async Task<Dictionary<TopicPartition, PartitionWatermark>> ReadWatermarksAsync(
        IAdminClient admin,
        IReadOnlyList<TopicPartition> partitions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lowOffsets = await ReadOffsetsAsync(admin, partitions, OffsetSpec.Earliest(), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var highOffsets = await ReadOffsetsAsync(admin, partitions, OffsetSpec.Latest(), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        return partitions
            .Where(partition => lowOffsets.ContainsKey(partition) && highOffsets.ContainsKey(partition))
            .ToDictionary(
                partition => partition,
                partition => new PartitionWatermark(lowOffsets[partition], highOffsets[partition]));
    }

    private async Task<Dictionary<TopicPartition, long>> ReadOffsetsAsync(
        IAdminClient admin,
        IReadOnlyList<TopicPartition> partitions,
        OffsetSpec offsetSpec,
        CancellationToken cancellationToken)
    {
        var specs = partitions.Select(partition => new TopicPartitionOffsetSpec
        {
            TopicPartition = partition,
            OffsetSpec = offsetSpec
        });

        var result = await KafkaNativeRequestAwaiter.AwaitAsync(
            admin.ListOffsetsAsync(specs, new ListOffsetsOptions
            {
                RequestTimeout = Option.AdminRequestTimeout
            }),
            cancellationToken);

        return result.ResultInfos
            .Where(info => info.TopicPartitionOffsetError.Error.Code == ErrorCode.NoError &&
                           info.TopicPartitionOffsetError.Offset.Value >= 0)
            .ToDictionary(
                info => info.TopicPartitionOffsetError.TopicPartition,
                info => info.TopicPartitionOffsetError.Offset.Value);
    }

    private static IReadOnlyList<TopicPartitionOffset> BuildAssignments(
        IReadOnlyDictionary<TopicPartition, PartitionWatermark> watermarks,
        int maxMessages)
    {
        var assignments = new List<TopicPartitionOffset>(watermarks.Count);
        foreach (var (partition, watermark) in watermarks)
        {
            var low = watermark.Low;
            var high = watermark.High;
            if (high <= low)
            {
                continue;
            }

            assignments.Add(new TopicPartitionOffset(
                partition,
                new Offset(Math.Max(low, high - maxMessages))));
        }

        return assignments;
    }

    private IReadOnlyList<KafkaTopicMessageSample> ConsumePreview(
        IConsumer<byte[], byte[]> consumer,
        IReadOnlyList<TopicPartitionOffset> assignments,
        IReadOnlyDictionary<TopicPartition, PartitionWatermark> watermarks,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        consumer.Assign(assignments);

        var results = new List<KafkaTopicMessageSample>(maxMessages);
        var highWatermarks = assignments.ToDictionary(
            assignment => assignment.TopicPartition,
            assignment => watermarks[assignment.TopicPartition].High);
        var deadline = DateTimeOffset.UtcNow.Add(Option.MessagePreviewTimeout);

        while (results.Count < maxMessages &&
               DateTimeOffset.UtcNow < deadline &&
               highWatermarks.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = consumer.Consume(TimeSpan.FromMilliseconds(250));
            if (result is null)
            {
                continue;
            }

            MarkConsumedPartition(result, highWatermarks);
            if (result.IsPartitionEOF || result.Message is null)
            {
                continue;
            }

            results.Add(MapMessage(result));
        }

        consumer.Unassign();

        return results
            .OrderByDescending(message => message.Timestamp ?? DateTimeOffset.MinValue)
            .ThenByDescending(message => message.Partition)
            .ThenByDescending(message => message.Offset)
            .Take(maxMessages)
            .ToList();
    }

    private static void MarkConsumedPartition(
        ConsumeResult<byte[], byte[]> result,
        Dictionary<TopicPartition, long> highWatermarks)
    {
        if (!highWatermarks.TryGetValue(result.TopicPartition, out var high))
        {
            return;
        }

        if (result.Offset.Value + 1 >= high)
        {
            highWatermarks.Remove(result.TopicPartition);
        }
    }

    private KafkaTopicMessageSample MapMessage(ConsumeResult<byte[], byte[]> result)
    {
        var maxValueBytes = Math.Max(1, Option.MessagePreviewMaxValueBytes);
        var value = Decode(result.Message.Value, maxValueBytes);
        var key = Decode(result.Message.Key, maxValueBytes);

        return new KafkaTopicMessageSample
        {
            TopicName = result.Topic,
            Partition = result.Partition.Value,
            Offset = result.Offset.Value,
            Timestamp = result.Message.Timestamp.UtcDateTime == DateTime.MinValue
                ? null
                : new DateTimeOffset(result.Message.Timestamp.UtcDateTime, TimeSpan.Zero),
            Key = key.Text,
            Value = value.Text,
            ValueEncoding = value.Encoding,
            ValueSizeBytes = result.Message.Value?.Length ?? 0,
            IsTruncated = value.IsTruncated,
            IsTombstone = result.Message.Value is null
        };
    }

    private static DecodedPayload Decode(byte[]? payload, int maxBytes)
    {
        if (payload is null)
        {
            return new DecodedPayload(null, "null", false);
        }

        var displayBytes = payload.Length > maxBytes
            ? payload.Take(maxBytes).ToArray()
            : payload;

        try
        {
            return new DecodedPayload(
                STRICT_UTF8.GetString(displayBytes),
                "utf-8",
                displayBytes.Length != payload.Length);
        }
        catch (DecoderFallbackException)
        {
            return new DecodedPayload(
                Convert.ToBase64String(displayBytes),
                "base64",
                displayBytes.Length != payload.Length);
        }
    }

    private sealed record DecodedPayload(string? Text, string Encoding, bool IsTruncated);

    private sealed record PartitionWatermark(long Low, long High);
}
