using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Models;
using Monica.StateStore;
using Monica.StateStore.StateStore.Abstractions;

namespace Monica.ServiceDiscovery.Services.Support;

/// <summary>
/// Registration state manager based on StateStore
/// </summary>
public class RegistrationStateManager(
    [FromKeyedServices(nameof(ModuleServiceDiscovery))] IStateStore stateStore,
    IServiceDiscoveryClientInfo clientInfo,
    ILeaderElectionService leaderElectionService,
    IOptions<ModuleServiceDiscoveryOption> options,
    ILogger<RegistrationStateManager> logger)
    : IRegistrationStateManager
{
    private readonly ModuleServiceDiscoveryOption _option = options.Value;

    private const string REG_PREFIX = "reg:";
    private const string LEADER_PREFIX = "leader:";

    // Cached registration time to avoid unnecessary Get operations on every heartbeat
    private DateTime? _cachedRegistrationTime;
    private readonly object _registrationTimeLock = new();

    /// <summary>
    /// Get registration key
    /// </summary>
    private string GetRegistrationKey() =>
        $"{(clientInfo.GetServiceStatus() is { } status ? $"{status.ServiceName}:{status.InstanceId}" : throw new InvalidOperationException("InstanceId is required"))}";

    /// <summary>
    /// Get Leader Key
    /// </summary>
    private string GetLeaderKey() => clientInfo.GetServiceStatus().ServiceName;

    public async Task<RegistrationResult> RegisterOrHeartbeatAsync(CancellationToken ct = default)
    {
        try
        {
            var regKey = GetRegistrationKey();
            var now = DateTime.UtcNow;

            // Get the base instance status (contains all service information and metadata)
            var instanceState = clientInfo.GetServiceStatus();

            // Use cached registration time to avoid Get operations for every heartbeat
            DateTime registrationTime;
            lock (_registrationTimeLock)
            {
                registrationTime = _cachedRegistrationTime ?? now;
            }

            // Set runtime status field
            instanceState.RegistrationTime = registrationTime;
            instanceState.LastHeartbeatTime = now;
            instanceState.IsLeader = leaderElectionService.IsLeader;

            await stateStore.SaveStateAsync(
                REG_PREFIX + regKey,
                instanceState,
                ct,
                _option.Election.RegistrationTTL);

            // Cache registration time after first successful save
            lock (_registrationTimeLock)
            {
                _cachedRegistrationTime ??= registrationTime;
            }

            logger.LogDebug("心跳成功: {Key}", regKey);
            return new RegistrationResult(true, now, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "心跳失败");
            return new RegistrationResult(false, null, ex.Message);
        }
    }

    public async Task<bool> LeaderExistsAsync(CancellationToken ct = default)
    {
        try
        {
            var leaderKey = GetLeaderKey();
            return await stateStore.ExistAsync(LEADER_PREFIX + leaderKey, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "检查 Leader Key 是否存在失败");
            return false;
        }
    }

    public async Task<LeaderState?> GetLeaderStateAsync(CancellationToken ct = default)
    {
        try
        {
            var leaderKey = GetLeaderKey();
            return await stateStore.GetStateAsync<LeaderState>(LEADER_PREFIX + leaderKey, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取 Leader 状态失败");
            return null;
        }
    }

    public async Task<(bool Success, LeaderState? State, string? ETag)> TryBecomeLeaderAsync(CancellationToken ct = default)
    {
        try
        {
            var leaderKey = GetLeaderKey();
            var now = DateTime.UtcNow;
            var serviceStatus = clientInfo.GetServiceStatus();

            var leaderState = new LeaderState
            {
                InstanceId = serviceStatus.InstanceId,
                BecomeLeaderTime = now,
                ServiceName = serviceStatus.ServiceName
            };

            // Try to save only if Key does not exist
            var success = await stateStore.TrySaveStateIfNotExistsAsync(
                LEADER_PREFIX + leaderKey,
                leaderState,
                ct,
                _option.Election.LeaderTTL);

            if (success)
            {
                // Get ETag
                var (_, eTag) = await stateStore.GetStateAndETagAsync<LeaderState>(LEADER_PREFIX + leaderKey, ct);
                logger.LogInformation("成功成为 Leader: {InstanceId}", serviceStatus.InstanceId);
                return (true, leaderState, eTag);
            }

            logger.LogDebug("Leader 竞争失败，Key 已存在");
            return (false, null, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "尝试成为 Leader 失败");
            return (false, null, null);
        }
    }

    public async Task<(bool Success, string? NewETag, LeaderState? ActualState, string? ActualETag)> RenewLeaderLeaseAsync(string expectedETag, CancellationToken ct = default)
    {
        try
        {
            var leaderKey = GetLeaderKey();
            var now = DateTime.UtcNow;
            var serviceStatus = clientInfo.GetServiceStatus();

            var leaderState = new LeaderState
            {
                InstanceId = serviceStatus.InstanceId,
                BecomeLeaderTime = now,
                ServiceName = serviceStatus.ServiceName
            };

            var (success, newETag) = await stateStore.TrySaveStateWithETagAsync(
                LEADER_PREFIX + leaderKey,
                leaderState,
                expectedETag,
                ct,
                _option.Election.LeaderTTL);

            if (success)
            {
                logger.LogDebug("Leader 续约成功: {ETag}", newETag);
                return (true, newETag, null, null);
            }

            
            var (actualState, actualETag) = await stateStore.GetStateAndETagAsync<LeaderState>(LEADER_PREFIX + leaderKey, ct);
            if(actualState is not null)
                logger.LogWarning("Leader 续约失败，ETag 不匹配。本地 ETag: {ExpectedETag}, 实际 ETag: {ActualETag}",
                expectedETag, actualETag);
            else 
                logger.LogWarning("Leader 续约失败，未找到 Leader 状态");
            return (false, null, actualState, actualETag);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Leader 续约异常");
            return (false, null, null, null);
        }
    }

    public async Task DeleteLeaderKeyAsync(CancellationToken ct = default)
    {
        try
        {
            var leaderKey = GetLeaderKey();
            await stateStore.DeleteStateAsync(LEADER_PREFIX + leaderKey, ct);
            logger.LogInformation("已删除 Leader Key: {Key}", leaderKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "删除 Leader Key 失败");
        }
    }

    public async Task ForceDeleteLeaderKeyAsync(string serviceName, CancellationToken ct = default)
    {
        try
        {
            await stateStore.DeleteStateAsync(LEADER_PREFIX + serviceName, ct);
            logger.LogInformation("已强制删除 Leader Key: {ServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "强制删除 Leader Key 失败: {ServiceName}", serviceName);
            throw;
        }
    }

    public async Task<List<InstanceState>> GetAllInstancesAsync(CancellationToken ct = default)
    {
        try
        {
            // Step 1: Use glob pattern to scan all keys under the reg prefix
            var instanceKeys = await stateStore.ScanKeysAsync(REG_PREFIX + "*", ct);

            if (instanceKeys.Count == 0)
            {
                logger.LogDebug("未找到任何实例");
                return [];
            }

            // Step 2: Get all InstanceState in batches
            var instances = await stateStore.GetBulkStateAsync<InstanceState>(
                instanceKeys,
                removeEmptyValue: true,
                cancellationToken: ct);
            
            return instances.Values
                .Where(x => x != null)
                .ToList()!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取所有实例失败");
            return [];
        }
    }

    public async Task<List<InstanceState>> GetAllLeaderInstancesAsync(CancellationToken ct = default)
    {
        try
        {
            // Step 1: Use glob pattern to scan all keys under the leader prefix
            var leaderKeys = await stateStore.ScanKeysAsync(LEADER_PREFIX + "*", ct);

            if (leaderKeys.Count == 0)
            {
                logger.LogDebug("未找到任何 Leader");
                return [];
            }

            // Step 2: Get all LeaderState in batches
            var leaderStates = await stateStore.GetBulkStateAsync<LeaderState>(
                leaderKeys,
                removeEmptyValue: true,
                cancellationToken: ct);

            // Step 3: Build the key list of InstanceState
            var instanceKeys = leaderStates.Values
                .Where(ls => ls != null && !string.IsNullOrEmpty(ls.ServiceName))
                .Select(ls => REG_PREFIX + $"{ls!.ServiceName}:{ls.InstanceId}")
                .ToList();

            if (instanceKeys.Count == 0)
            {
                logger.LogDebug("未找到有效的 Leader 实例键");
                return [];
            }

            // Step 4: Get the InstanceState of all Leaders in batches
            var instances = await stateStore.GetBulkStateAsync<InstanceState>(
                instanceKeys,
                removeEmptyValue: true,
                cancellationToken: ct);

            return instances.Values.Where(x => x != null).ToList()!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取所有 Leader 实例失败");
            return [];
        }
    }
}
