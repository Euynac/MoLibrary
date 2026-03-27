using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Monica.AI.Extensions;

/// <summary>
/// Streaming response extension methods
/// </summary>
public static class StreamingExtensions
{
    /// <summary>
    /// Convert AgentResponseUpdate stream to SSE items
    /// </summary>
    public static async IAsyncEnumerable<SseItem<string>> ToSseItems(
        this IAsyncEnumerable<AgentResponseUpdate> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var update in source.WithCancellation(ct))
        {
            var data = new StreamingChatData
            {
                Text = update.Text
            };

            yield return new SseItem<string>(JsonSerializer.Serialize(data, JsonOptions), "message");
        }

        yield return new SseItem<string>(JsonSerializer.Serialize(new StreamingChatData { Done = true }, JsonOptions), "done");
    }

    /// <summary>
    /// Convert ChatResponseUpdate stream to SSE items
    /// </summary>
    public static async IAsyncEnumerable<SseItem<string>> ToSseItems(
        this IAsyncEnumerable<ChatResponseUpdate> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var update in source.WithCancellation(ct))
        {
            var data = new StreamingChatData
            {
                Text = update.Text,
                FinishReason = update.FinishReason?.ToString(),
                ModelId = update.ModelId
            };

            yield return new SseItem<string>(JsonSerializer.Serialize(data, JsonOptions), "message");
        }

        yield return new SseItem<string>(JsonSerializer.Serialize(new StreamingChatData { Done = true }, JsonOptions), "done");
    }

    /// <summary>
    /// Convert string stream to SSE items
    /// </summary>
    public static async IAsyncEnumerable<SseItem<string>> ToSseItems(
        this IAsyncEnumerable<string> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var text in source.WithCancellation(ct))
        {
            if (!string.IsNullOrEmpty(text))
            {
                var data = new StreamingChatData { Text = text };
                yield return new SseItem<string>(JsonSerializer.Serialize(data, JsonOptions), "message");
            }
        }

        yield return new SseItem<string>(JsonSerializer.Serialize(new StreamingChatData { Done = true }, JsonOptions), "done");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>
/// Streaming chat data
/// </summary>
public class StreamingChatData
{
    /// <summary>
    /// text content
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Completion reason
    /// </summary>
    public string? FinishReason { get; set; }

    /// <summary>
    /// Model ID
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// Is it done?
    /// </summary>
    public bool Done { get; set; }
}
