namespace Monica.DataChannel.Models
{
    public sealed record MessageReceiveActive(
        string ReceiveId,
        string TraceIdentifier,
        int ThreadId,
        DateTimeOffset StartedAtUtc);
}
