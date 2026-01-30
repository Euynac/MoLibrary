namespace Monica.RegisterCentre.Events;

/// <summary>
/// Event published when service registration to the register centre completes (successfully or after all retries)
/// </summary>
public class ServiceRegistrationCompletedEvent
{
    /// <summary>
    /// Whether the registration was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The AppId of the registered service
    /// </summary>
    public required string AppId { get; init; }

    /// <summary>
    /// Message describing the registration result
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Number of retry attempts made before completion
    /// </summary>
    public required int RetryCount { get; init; }

    /// <summary>
    /// Timestamp when the registration completed
    /// </summary>
    public required DateTime CompletedAt { get; init; }
}
