using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Abstractions.Subscriptions;
using MoLibrary.FrameworkUI.UIEventBus.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.FrameworkUI.UIEventBus.Services;

/// <summary>
/// 事件总线测试服务，用于管理测试订阅和消息收集
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
    /// 是否已订阅
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
    /// 当前订阅的主题名称
    /// </summary>
    public string? CurrentTopicName { get; private set; }

    /// <summary>
    /// 发布测试消息
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
    /// 订阅测试主题
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

            // 订阅主题
            var subscription = await distributedEventBus.SubscribeAsync<TestEventMessage>(async eventData =>
            {
                var receivedMessage = new ReceivedTestMessage
                {
                    EventData = eventData,
                    ReceivedAt = DateTime.Now, // 使用本地时间
                    TopicName = topicName
                };

                _receivedMessages.Enqueue(receivedMessage);

                // 限制队列大小
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
    /// 取消订阅测试主题
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
    /// 获取接收到的消息列表
    /// </summary>
    public List<ReceivedTestMessage> GetReceivedMessages()
    {
        return _receivedMessages.ToList();
    }

    /// <summary>
    /// 清空接收到的消息
    /// </summary>
    public void ClearReceivedMessages()
    {
        _receivedMessages.Clear();
        logger.LogInformation("已清空接收到的测试消息");
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await UnsubscribeFromTestTopicAsync();
        _receivedMessages.Clear();
        logger.LogInformation("EventBusTestService 已释放");
    }
}

/// <summary>
/// 接收到的测试消息
/// </summary>
public class ReceivedTestMessage
{
    /// <summary>
    /// 事件数据
    /// </summary>
    public required TestEventMessage EventData { get; init; }

    /// <summary>
    /// 接收时间（本地时间）
    /// </summary>
    public DateTime ReceivedAt { get; init; }

    /// <summary>
    /// 主题名称
    /// </summary>
    public required string TopicName { get; init; }
}
