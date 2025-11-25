namespace MoLibrary.RegisterCentre.Interfaces;

/// <summary>
/// Coordinates service registration lifecycle and provides synchronization for dependent services
/// </summary>
public interface IServiceRegistrationCoordinator
{
    /// <summary>
    /// Gets the current registration status
    /// </summary>
    RegistrationStatus Status { get; }

    /// <summary>
    /// Gets whether the service is successfully registered
    /// </summary>
    bool IsRegistered { get; }

    /// <summary>
    /// Waits for registration to complete or timeout
    /// </summary>
    /// <param name="timeout">Maximum time to wait for registration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if registration completed successfully, false if timed out or failed</returns>
    Task<bool> WaitForRegistrationAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the status of service registration
/// </summary>
public enum RegistrationStatus
{
    /// <summary>
    /// Registration has not started yet
    /// </summary>
    NotStarted,

    /// <summary>
    /// Registration is in progress
    /// </summary>
    InProgress,

    /// <summary>
    /// Registration completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Registration failed after all retry attempts
    /// </summary>
    Failed
}
