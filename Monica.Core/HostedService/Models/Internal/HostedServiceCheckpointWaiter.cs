namespace Monica.Core.HostedService.Models.Internal;

internal sealed class HostedServiceCheckpointWaiter(DateTime? notBeforeUtc)
{
    public DateTime? NotBeforeUtc { get; } = notBeforeUtc;

    public TaskCompletionSource<bool> CompletionSource { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
