namespace Monica.DataChannel.Models
{
    public sealed record MessageReceiveRateSnapshot(
        int CurrentSecondMessageCount,
        int LastSecondCompletedMessageCount);
}
