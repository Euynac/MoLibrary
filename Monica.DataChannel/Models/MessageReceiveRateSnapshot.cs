namespace Monica.DataChannel.Models.DaprBinding
{
    public sealed record MessageReceiveRateSnapshot(
        int CurrentSecondMessageCount,
        int LastSecondCompletedMessageCount);
}
