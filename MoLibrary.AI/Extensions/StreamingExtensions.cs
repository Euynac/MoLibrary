using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace MoLibrary.AI.Extensions;

/// <summary>
/// 流式响应扩展方法
/// </summary>
public static class StreamingExtensions
{
    /// <summary>
    /// 将 ChatResponseUpdate 流转换为 SSE 项流
    /// </summary>
    /// <param name="source">ChatResponseUpdate 流</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>SseItem 流</returns>
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

        // 发送完成事件
        yield return new SseItem<string>(JsonSerializer.Serialize(new StreamingChatData { Done = true }, JsonOptions), "done");
    }

    /// <summary>
    /// 将字符串流转换为 SSE 项流
    /// </summary>
    /// <param name="source">字符串流</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>SseItem 流</returns>
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
/// 流式聊天数据
/// </summary>
public class StreamingChatData
{
    /// <summary>
    /// 文本内容
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// 完成原因
    /// </summary>
    public string? FinishReason { get; set; }

    /// <summary>
    /// 模型 ID
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// 是否完成
    /// </summary>
    public bool Done { get; set; }
}
