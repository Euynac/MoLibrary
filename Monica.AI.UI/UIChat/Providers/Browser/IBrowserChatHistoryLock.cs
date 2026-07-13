namespace Monica.AI.UI.UIChat.Providers.Browser;

/// <summary>
/// Coordinates browser-history mutations across every tab on the current origin.
/// </summary>
internal interface IBrowserChatHistoryLock
{
    /// <summary>Acquires an exclusive origin-wide lease for the supplied logical scope.</summary>
    ValueTask<IAsyncDisposable> AcquireAsync(string scope, CancellationToken ct = default);
}
