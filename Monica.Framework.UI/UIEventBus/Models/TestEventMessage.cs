namespace Monica.Framework.UI.UIEventBus.Models;

/// <summary>
/// Test event messages for distributed event bus testing
/// </summary>
public class TestEventMessage
{
    /// <summary>
    /// Message content
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Message creation time (UTC)
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Message ID, used for tracking
    /// </summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
}
