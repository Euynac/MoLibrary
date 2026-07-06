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

    public Task<KafkaTopicMessageBatch> ReadMessagesAsync(
        KafkaClusterConfig cluster,
        KafkaTopicMessagesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TopicName);

        var maxMessages = Math.Clamp(request.MaxMessages, 1, Math.Max(1, Option.MessagePreviewMaxMessages));
        using var admin = CreateAdminClient(cluster);
        using var consumer = CreateConsumer(cluster);
        var topicName = request.TopicName.Trim();
        var partitions = ResolvePartitions(admin, topicName);
        var assignments = BuildAssignments(consumer, partitions, maxMessages);
        var messages = assignments.Count == 0
            ? []
            : ConsumePreview(consumer, assignments, maxMessages, cancellationToken);

        return Task.FromResult(new KafkaTopicMessageBatch
        {
            ClusterId = cluster.ClusterId,
            TopicName = topicName,
            MaxMessages = maxMessages,
            CapturedAt = DateTimeOffset.UtcNow,
            Messages = messages
        });
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

    private IReadOnlyList<TopicPartitionOffset> BuildAssignments(
        IConsumer<byte[], byte[]> consumer,
        IReadOnlyList<TopicPartition> partitions,
        int maxMessages)
    {
        var assignments = new List<TopicPartitionOffset>(partitions.Count);
        foreach (var partition in partitions)
        {
            var watermark = consumer.QueryWatermarkOffsets(partition, Option.AdminRequestTimeout);
            var low = watermark.Low.Value;
            var high = watermark.High.Value;
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
        int maxMessages,
        CancellationToken cancellationToken)
    {
        consumer.Assign(assignments);

        var results = new List<KafkaTopicMessageSample>(maxMessages);
        var highWatermarks = assignments.ToDictionary(
            assignment => assignment.TopicPartition,
            assignment => consumer.QueryWatermarkOffsets(assignment.TopicPartition, Option.AdminRequestTimeout).High.Value);
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
}
