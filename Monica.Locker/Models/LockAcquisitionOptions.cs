namespace Monica.Locker.Models;

public sealed class LockAcquisitionOptions
{
    public string? Owner { get; init; }

    public TimeSpan? WaitTimeout { get; init; }

    public TimeSpan? LeaseDuration { get; init; }
}
