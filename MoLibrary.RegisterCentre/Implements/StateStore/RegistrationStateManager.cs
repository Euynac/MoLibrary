using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.StateStore;

namespace MoLibrary.RegisterCentre.Implements.StateStore;

/// <summary>
/// 基于 StateStore 的注册状态管理器
/// </summary>
public class RegistrationStateManager(
    [FromKeyedServices(nameof(ModuleRegisterCentre))] IMoStateStore stateStore,
    IRegisterCentreClientInfo clientInfo,
    ILeaderElectionService leaderElectionService,
    IOptions<ModuleRegisterCentreOption> options,
    ILogger<RegistrationStateManager> logger) : IRegistrationStateManager
{
    private const string REG_PREFIX = "reg";
    private const string LEADER_PREFIX = "leader";

    private readonly ModuleRegisterCentreOption _option = options.Value;

    /// <summary>
    /// 获取注册 Key
    /// </summary>
    private string GetRegistrationKey() =>
        $"{(clientInfo.GetServiceStatus() is { } status ? $"{status.ServiceName}:{status.InstanceId}" : throw new InvalidOperationException("InstanceId is required"))}";

    /// <summary>
    /// 获取 Leader Key
    /// </summary>
    private string GetLeaderKey() => clientInfo.GetServiceStatus().ServiceName;

    public async Task<RegistrationResult> RegisterOrHeartbeatAsync(CancellationToken ct = default)
    {
        try
        {
            var regKey = GetRegistrationKey();
            var now = DateTime.UtcNow;

            // 获取基础实例状态（包含所有服务信息和元数据）
            var instanceState = clientInfo.GetServiceStatus();

            // 获取现有状态以保留原始注册时间
            var existingState = await stateStore.GetStateAsync<InstanceState>(regKey, REG_PREFIX, ct);

            // 设置运行时状态字段
            instanceState.RegistrationTime = existingState?.RegistrationTime ?? now;
            instanceState.LastHeartbeatTime = now;
            instanceState.IsLeader = leaderElectionService.IsLeader;

            await stateStore.SaveStateAsync(
                regKey,
                instanceState,
                REG_PREFIX,
                ct,
                _option.Election.RegistrationTTL);

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
            return await stateStore.ExistAsync<LeaderState>(leaderKey, LEADER_PREFIX, ct);
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
            return await stateStore.GetStateAsync<LeaderState>(leaderKey, LEADER_PREFIX, ct);
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

            // 尝试仅在 Key 不存在时保存
            var success = await stateStore.TrySaveStateIfNotExistsAsync(
                leaderKey,
                leaderState,
                LEADER_PREFIX,
                ct,
                _option.Election.LeaderTTL);

            if (success)
            {
                // 获取 ETag
                var (_, eTag) = await stateStore.GetStateAndVersionAsync<LeaderState>(leaderKey, LEADER_PREFIX, ct);
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

    public async Task<(bool Success, string? NewETag)> RenewLeaderLeaseAsync(string expectedETag, CancellationToken ct = default)
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
                leaderKey,
                leaderState,
                expectedETag,
                LEADER_PREFIX,
                ct,
                _option.Election.LeaderTTL);

            if (success)
            {
                logger.LogDebug("Leader 续约成功: {ETag}", newETag);
                return (true, newETag);
            }

            logger.LogWarning("Leader 续约失败，ETag 不匹配");
            return (false, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Leader 续约异常");
            return (false, null);
        }
    }

    public async Task DeleteLeaderKeyAsync(CancellationToken ct = default)
    {
        try
        {
            var leaderKey = GetLeaderKey();
            await stateStore.DeleteStateAsync(leaderKey, LEADER_PREFIX, ct);
            logger.LogInformation("已删除 Leader Key: {Key}", leaderKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "删除 Leader Key 失败");
        }
    }

    public async Task<List<InstanceState>> GetAllInstancesAsync(CancellationToken ct = default)
    {
        try
        {
            // 步骤 1：获取所有 reg 前缀下的 key
            var instanceKeys = await stateStore.GetAllKeysByPrefixAsync(REG_PREFIX, ct);

            if (instanceKeys.Count == 0)
            {
                logger.LogDebug("未找到任何实例");
                return [];
            }

            // 步骤 2：批量获取所有 InstanceState
            var instances = await stateStore.GetBulkStateAsync<InstanceState>(
                instanceKeys,
                REG_PREFIX,
                removePrefix: true,
                removeEmptyValue: true,
                ct);

            var serviceName = clientInfo.GetServiceStatus().ServiceName;

            // 步骤 3：过滤当前服务的实例，并按 LastHeartbeatTime 降序排序
            return instances.Values
                .Where(x => x != null && x.ServiceName == serviceName)
                .OrderByDescending(x => x.LastHeartbeatTime)
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
            // 步骤 1：获取所有 leader 前缀下的 key
            var leaderKeys = await stateStore.GetAllKeysByPrefixAsync(LEADER_PREFIX, ct);

            if (leaderKeys.Count == 0)
            {
                logger.LogDebug("未找到任何 Leader");
                return [];
            }

            // 步骤 2：批量获取所有 LeaderState
            var leaderStates = await stateStore.GetBulkStateAsync<LeaderState>(
                leaderKeys,
                LEADER_PREFIX,
                removePrefix: true,
                removeEmptyValue: true,
                ct);

            // 步骤 3：构建 InstanceState 的 key 列表
            var instanceKeys = leaderStates.Values
                .Where(ls => ls != null && !string.IsNullOrEmpty(ls.ServiceName))
                .Select(ls => $"{ls!.ServiceName}:{ls.InstanceId}")
                .ToList();

            if (instanceKeys.Count == 0)
            {
                logger.LogDebug("未找到有效的 Leader 实例键");
                return [];
            }

            // 步骤 4：批量获取所有 Leader 的 InstanceState
            var instances = await stateStore.GetBulkStateAsync<InstanceState>(
                instanceKeys,
                REG_PREFIX,
                removePrefix: true,
                removeEmptyValue: true,
                ct);

            return instances.Values.Where(x => x != null).ToList()!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取所有 Leader 实例失败");
            return [];
        }
    }
}
