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
    IOptions<ModuleRegisterCentreOption> options,
    ILogger<RegistrationStateManager> logger) : IRegistrationStateManager
{
    private const string REG_PREFIX = "reg";
    private const string LEADER_PREFIX = "leader";

    private readonly ModuleRegisterCentreOption _option = options.Value;

    /// <summary>
    /// 获取注册 Key
    /// </summary>
    private string GetRegistrationKey() => $"{_option.AppId}:{_option.FromInstance}";

    /// <summary>
    /// 获取 Leader Key
    /// </summary>
    private string GetLeaderKey() => _option.AppId ?? throw new InvalidOperationException("AppId is required");

    public async Task<RegistrationResult> RegisterOrHeartbeatAsync(CancellationToken ct = default)
    {
        try
        {
            var regKey = GetRegistrationKey();
            var now = DateTime.UtcNow;

            var instanceState = new InstanceState
            {
                ServiceName = _option.AppId!,
                InstanceId = _option.FromInstance!,
                RegistrationTime = now,
                LastHeartbeatTime = now,
                RegisterInfo = clientInfo.GetServiceStatus()
            };

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

            var leaderState = new LeaderState
            {
                InstanceId = _option.FromInstance!,
                BecomeLeaderTime = now,
                ServiceName = _option.AppId
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
                logger.LogInformation("成功成为 Leader: {InstanceId}", _option.FromInstance);
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

            var leaderState = new LeaderState
            {
                InstanceId = _option.FromInstance!,
                BecomeLeaderTime = now,
                ServiceName = _option.AppId
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
        // 在分布式模式下，需要通过查询获取所有实例
        // 这里简化实现，只返回当前实例
        // 完整实现需要使用 QueryStateAsync 或其他机制
        try
        {
            var regKey = GetRegistrationKey();
            var state = await stateStore.GetStateAsync<InstanceState>(regKey, REG_PREFIX, ct);
            return state != null ? [state] : [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取所有实例失败");
            return [];
        }
    }

    public Task<List<InstanceState>> GetAllLeaderInstancesAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
