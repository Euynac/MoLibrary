namespace Monica.JobScheduler.Models;

/// <summary>
/// Result of execution slot reservation attempt.
/// </summary>
public sealed class ReservationResult
{
    public bool Reserved { get; private init; }
    public string? Reason { get; private init; }

    public static ReservationResult Success() => new() { Reserved = true };

    public static ReservationResult Failure(string reason) => new() { Reserved = false, Reason = reason };
}
