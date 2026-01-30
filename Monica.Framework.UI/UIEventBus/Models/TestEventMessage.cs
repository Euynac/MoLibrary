namespace Monica.Framework.UI.UIEventBus.Models;

/// <summary>
/// 测试事件消息，用于分布式事件总线测试
/// </summary>
public class TestEventMessage
{
    /// <summary>
    /// 消息内容
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 消息创建时间（UTC）
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 消息ID，用于追踪
    /// </summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
}
