namespace Monica.Core.HostedService.Models.Internal;

internal sealed class HostedServiceCheckpointState
{
    public DateTime? LastOccurredAtUtc { get; set; }

    public List<HostedServiceCheckpointWaiter> Waiters { get; } = [];
}
