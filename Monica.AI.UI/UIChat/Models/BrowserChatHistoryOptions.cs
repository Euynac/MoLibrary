namespace Monica.AI.UI.UIChat.Models;

/// <summary>
/// Configures durable chat history stored in the current browser profile.
/// </summary>
public sealed class BrowserChatHistoryOptions
{
    /// <summary>
    /// Gets or sets the maximum number of conversations retained in browser storage.
    /// </summary>
    /// <remarks>
    /// The default is 30. When retention or browser quota requires pruning, the currently
    /// selected conversation is never removed automatically.
    /// </remarks>
    public int MaxSessions { get; set; } = 30;
}
