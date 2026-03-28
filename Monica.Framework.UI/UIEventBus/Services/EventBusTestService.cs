using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Subscriptions;
using Monica.Framework.UI.UIEventBus.Models;
using Monica.Core.Results;

namespace Monica.Framework.UI.UIEventBus.Services;

/// <summary>
/// Event bus test service for managing test subscriptions and message collection
/// </summary>
public sealed class EventBusTestService(
    IMoDistributedEventBus distributedEventBus,
    ILogger<EventBusTestService> logger) : IAsyncDisposable
{
    private const int MaxMessageCount = 100;

    private readonly ConcurrentQueue<ReceivedTestMessage> _receivedMessages = new();
    private readonly object _subscriptionLock = new();
    private ISubscription? _activeSubscription;

    /// <summary>
    /// Have you subscribed
    /// </summary>
    public bool IsSubscribed
    {
        get
        {
            lock (_subscriptionLock)
            {
                return _activeSubscription != null;
            }
        }
    }

    /// <summary>
    /// The name of the currently subscribed topic
    /// </summary>
    public string? CurrentTopicName { get; private set; }

    /// <summary>
    /// Publish test message
    /// </summary>
    public async Task<Res> PublishTestMessageAsync(string message, string? topicName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return Res.Fail("消息内容不能为空");
            }

            if (string.IsNullOrWhiteSpace(topicName))
            {
                return Res.Fail("主题名称不能为空");
            }

            var testMessage = new TestEventMessage
            {
                Message = message,
                CreatedAt = DateTime.UtcNow,
                MessageId = Guid.NewGuid().ToString()
            };

            await distributedEventBus.PublishAsync(testMessage, topicName, cancellationToken);

            logger.LogInformation("已发布测试消息到主题 {TopicName}: {MessageId}", topicName, testMessage.MessageId);

            return Res.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "发布测试消息失败");
            return Res.Fail($"发布失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Subscribe to test topic
    /// </summary>
    public async Task<Res<ISubscription>> SubscribeToTestTopicAsync(string topicName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(topicName))
            {
                return Res.Fail("主题名称不能为空");
            }

            lock (_subscriptionLock)
            {
                if (_activeSubscription != null)
                {
                    return Res.Fail("已存在活动订阅，请先停止当前订阅");
                }
            }

            // Subscribe to topics
            var subscription = await distributedEventBus.SubscribeAsync<TestEventMessage>(async eventData =>
            {
                var receivedMessage = new ReceivedTestMessage
                {
                    EventData = eventData,
                    ReceivedAt = DateTime.Now, // 使用本地时间
                    TopicName = topicName
                };

                _receivedMessages.Enqueue(receivedMessage);

                // Limit queue size
                while (_receivedMessages.Count > MaxMessageCount)
                {
                    _receivedMessages.TryDequeue(out _);
                }

                logger.LogInformation("收到测试消息: {MessageId} from topic {TopicName}",
                    eventData.MessageId, topicName);

                await Task.CompletedTask;
            }, topicName);

            lock (_subscriptionLock)
            {
                _activeSubscription = subscription;
                CurrentTopicName = topicName;
            }

            logger.LogInformation("已订阅测试主题: {TopicName}", topicName);
            return Res.Ok(_activeSubscription);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "订阅测试主题失败");
            return Res.Fail($"订阅失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Unsubscribe from test topic
    /// </summary>
    public async Task<Res> UnsubscribeFromTestTopicAsync()
    {
        try
        {
            ISubscription? subscription;

            lock (_subscriptionLock)
            {
                subscription = _activeSubscription;
                _activeSubscription = null;
                CurrentTopicName = null;
            }

            if (subscription != null)
            {
                await subscription.DisposeAsync();
                logger.LogInformation("已取消订阅测试主题");
            }

            return Res.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "取消订阅失败");
            return Res.Fail($"取消订阅失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Get a list of received messages
    /// </summary>
    public List<ReceivedTestMessage> GetReceivedMessages()
    {
        return _receivedMessages.ToList();
    }

    /// <summary>
    /// Clear received messages
    /// </summary>
    public void ClearReceivedMessages()
    {
        _receivedMessages.Clear();
        logger.LogInformation("已清空接收到的测试消息");
    }

    /// <summary>
    /// Release resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await UnsubscribeFromTestTopicAsync();
        _receivedMessages.Clear();
        logger.LogInformation("EventBusTestService 已释放");
    }
}

/// <summary>
/// Test message received
/// </summary>
public class ReceivedTestMessage
{
    /// <summary>
    /// event data
    /// </summary>
    public required TestEventMessage EventData { get; init; }

    /// <summary>
    /// Receive time (local time)
    /// </summary>
    public DateTime ReceivedAt { get; init; }

    /// <summary>
    /// Topic name
    /// </summary>
    public required string TopicName { get; init; }
}
