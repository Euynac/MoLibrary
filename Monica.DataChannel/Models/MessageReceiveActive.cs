namespace Monica.DataChannel.Models.DaprBinding
{
    public sealed record MessageReceiveActive(
        string ReceiveId,
        string TraceIdentifier,
        int ThreadId,
        DateTimeOffset StartedAtUtc);
}
