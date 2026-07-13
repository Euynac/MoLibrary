using Monica.AI.Chat.Abstractions;
using Monica.AI.Chat.Models;
using Monica.UI.Shell.Support;

namespace Monica.AI.UI.UIChat.Providers.Browser;

internal sealed class BrowserChatHistoryPartitionResolver(
    IBrowserStorage browserStorage,
    IBrowserChatHistoryLock browserLock) : IChatHistoryPartitionResolver, IDisposable
{
    private const string INSTALLATION_ID_KEY = "ai-chat-history:installation-id";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private ChatHistoryPartition? _partition;

    public async ValueTask<ChatHistoryPartition> ResolveAsync(CancellationToken ct = default)
    {
        if (_partition is not null)
        {
            return _partition;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_partition is not null)
            {
                return _partition;
            }

            await using var browserLease = await browserLock.AcquireAsync(INSTALLATION_ID_KEY, ct);
            var installationId = await browserStorage.GetAsync<string?>(INSTALLATION_ID_KEY, null);
            if (string.IsNullOrWhiteSpace(installationId))
            {
                installationId = Guid.NewGuid().ToString("N");
                var writeResult = await browserStorage.TrySetAsync(INSTALLATION_ID_KEY, installationId);
                if (!writeResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Browser chat history cannot persist a stable installation identifier.");
                }
            }

            _partition = new ChatHistoryPartition(installationId);
            return _partition;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
