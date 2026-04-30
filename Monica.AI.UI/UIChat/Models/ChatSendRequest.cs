using Monica.AI.AgentCapabilities.Models;

namespace Monica.AI.UI.UIChat.Models;

/// <summary>
/// Message submitted from the chat composer, including explicit per-turn capability references.
/// </summary>
public sealed record ChatSendRequest(
    string Message,
    IReadOnlyList<AgentCapabilityReference> CapabilityReferences);
