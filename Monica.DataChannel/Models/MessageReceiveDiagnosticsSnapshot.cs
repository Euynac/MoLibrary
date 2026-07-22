namespace Monica.DataChannel.Models
{
    public sealed record MessageReceiveDiagnosticsSnapshot(
        string ReceiveId,
        string Route,
        string TraceIdentifier,
        bool IsConcurrentReceive,
        int ActiveReceiveCount,
        long MaxConcurrentReceiveCount,
        int MessagePerSecond,
        int LastSecondCompletedMessageCount,
        long TotalReceivedMessageCount,
        int ThreadId,
        DateTimeOffset TimestampUtc
        );
}
