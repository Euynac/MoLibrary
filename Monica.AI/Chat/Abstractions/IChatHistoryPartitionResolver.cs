using Monica.AI.Chat.Models;

namespace Monica.AI.Chat.Abstractions;

/// <summary>Resolves the isolated partition for the current chat-history caller.</summary>
/// <remarks>
/// Resolvers are called for each facade operation. Server implementations should derive the key
/// from authenticated tenant and user identity and throw when that identity is unavailable.
/// </remarks>
public interface IChatHistoryPartitionResolver
{
    /// <summary>Resolves the current opaque storage partition.</summary>
    ValueTask<ChatHistoryPartition> ResolveAsync(CancellationToken ct = default);
}
