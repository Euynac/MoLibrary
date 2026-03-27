using Monica.ServiceDiscovery.Models;

namespace Monica.ServiceDiscovery.Abstractions;

/// <summary>
/// Registration results
/// </summary>
public record RegistrationResult(bool Success, DateTime? HeartbeatTime, string? ErrorMessage);

/// <summary>
/// Registration status management interface
/// </summary>
public interface IRegistrationStateManager
{
    /// <summary>
    /// Registration or heartbeat (the first registration is also a heartbeat, because inst_id will not be repeated)
    /// </summary>
    Task<RegistrationResult> RegisterOrHeartbeatAsync(CancellationToken ct = default);

    /// <summary>
    /// Check if Leader Key exists
    /// </summary>
    Task<bool> LeaderExistsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get the current Leader status
    /// </summary>
    Task<LeaderState?> GetLeaderStateAsync(CancellationToken ct = default);

    /// <summary>
    /// Attempt to become Leader (only if Leader Key does not exist)
    /// </summary>
    /// <returns>Returns (true, LeaderState) on success and (false, null) on failure.</returns>
    Task<(bool Success, LeaderState? State, string? ETag)> TryBecomeLeaderAsync(CancellationToken ct = default);

    /// <summary>
    /// Renew Leader (use ETag to verify)
    /// </summary>
    /// <param name="expectedETag">Expected ETag</param>
    /// <param name="ct">cancel token</param>
    /// <returns>
    /// Returns (true, NewETag, null, null) on success
    /// Returns (false, null, ActualState, ActualETag) on ​​failure - Contains the actual state and ETag in the current StateStore
    /// </returns>
    Task<(bool Success, string? NewETag, LeaderState? ActualState, string? ActualETag)> RenewLeaderLeaseAsync(string expectedETag, CancellationToken ct = default);

    /// <summary>
    /// Delete Leader Key (used for graceful shutdown)
    /// </summary>
    Task DeleteLeaderKeyAsync(CancellationToken ct = default);

    /// <summary>
    /// Get the status of all registered instances
    /// </summary>
    Task<List<InstanceState>> GetAllInstancesAsync(CancellationToken ct = default);
    /// <summary>
    /// Get all Leader instance status
    /// </summary>
    Task<List<InstanceState>> GetAllLeaderInstancesAsync(CancellationToken ct = default);

    /// <summary>
    /// Forcefully delete the Leader Key of the specified service (for management/debugging)
    /// </summary>
    Task ForceDeleteLeaderKeyAsync(string serviceName, CancellationToken ct = default);
}
