namespace Monica.AI.UI.UIChat.Providers.Browser;

internal sealed record BrowserChatHistoryCatalogHead(long CurrentRevision, long? PreviousRevision);

internal sealed record BrowserChatHistoryInlineDocument(string Payload, string Sha256);

internal sealed record BrowserChatHistorySnapshotManifest(int ChunkCount, string Sha256);
