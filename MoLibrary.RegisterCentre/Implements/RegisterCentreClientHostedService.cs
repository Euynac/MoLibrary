using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.HostedServices;
using MoLibrary.Core.Features.HostedServices.Models;
using MoLibrary.Core.Features.ObservableInstance;
using MoLibrary.Core.Modules;
using MoLibrary.RegisterCentre.Events;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.RegisterCentre.Modules;

namespace MoLibrary.RegisterCentre.Implements;

/// <summary>
/// 基于 StateStore 的注册中心客户端服务
/// 实现心跳、Leader 选举和挣扎逻辑
/// </summary>
public class RegisterCentreClientHostedService(
    IRegistrationStateManager stateManager,
    ILeaderElectionService leaderService,
    IRegisterCentreClientInfo clientInfo,
    ILogger<RegisterCentreClientHostedService> logger,
    IOptions<ModuleRegisterCentreOption> option,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions)
    : MoBackgroundService(observableManager, hostedServiceOptions, logger), IServiceRegistrationCoordinator
{
    private readonly ModuleRegisterCentreOption _option = option.Value;
    private readonly TaskCompletionSource<bool> _registrationCompletionSource = new();
    private readonly object _statusLock = new();
    private readonly Random _random = new();

    private RegistrationStatus _status = RegistrationStatus.NotStarted;
    private bool _isStruggling;
    private DateTime? _struggleStartTime;
    private CancellationTokenSource? _struggleCts;

    public override string ServiceName => "RegisterCentreClient";
    public override TimeSpan? HeartbeatInterval => null;

    public RegistrationStatus Status
    {
        get { lock (_statusLock) { return _status; } }
        private set { lock (_statusLock) { _status = value; } }
    }

    public bool IsRegistered => Status == RegistrationStatus.Completed;

    public async Task<bool> WaitForRegistrationAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await _registrationCompletionSource.Task.WaitAsync(cts.Token);
            return _registrationCompletionSource.Task.Result;
        }
        catch (OperationCanceledException)
        {
            RecordState($"等待注册完成超时 ({timeout})", givenLogLevel: LogLevel.Warning);
            return false;
        }
    }

    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        RecordState("开始 StateStore 心跳循环", HostedServiceState.Starting);
        Status = RegistrationStatus.InProgress;

        // 首次心跳
        var firstHeartbeat = await stateManager.RegisterOrHeartbeatAsync(stoppingToken);
        if (firstHeartbeat.Success)
        {
            Status = RegistrationStatus.Completed;
            _registrationCompletionSource.TrySetResult(true);
            RecordState("首次心跳成功", HostedServiceState.Running);
        }
        else
        {
            RecordState($"首次心跳失败: {firstHeartbeat.ErrorMessage}", givenLogLevel: LogLevel.Warning);
        }

        // 主循环
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 执行心跳
                var result = await stateManager.RegisterOrHeartbeatAsync(stoppingToken);

                if (result.Success)
                {
                    await HandleSuccessfulHeartbeatAsync(stoppingToken);
                }
                else
                {
                    await HandleFailedHeartbeatAsync(result.ErrorMessage, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                RecordState("心跳循环异常", HostedServiceState.Degraded, ex);
            }

            // 添加抖动的等待
            var jitter = _random.Next(
                -_option.Election.HeartbeatJitterMilliseconds,
                _option.Election.HeartbeatJitterMilliseconds);
            var delay = _option.Election.HeartbeatPeriod + TimeSpan.FromMilliseconds(jitter);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        // 优雅关闭 - 如果是 Leader，触发 LeaderLost
        if (leaderService.IsLeader)
        {
            RecordState("服务关闭，触发 Leader 丢失", givenLogLevel: LogLevel.Information);
            leaderService.TriggerLeaderLost(LeaderLostReason.GracefulShutdown);

            // 尝试删除 Leader Key，让其他实例更快接管
            try
            {
                await stateManager.DeleteLeaderKeyAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                RecordState("删除 Leader Key 失败", exception: ex, givenLogLevel: LogLevel.Warning);
            }
        }
    }

    /// <summary>
    /// 处理心跳成功的情况
    /// </summary>
    private async Task HandleSuccessfulHeartbeatAsync(CancellationToken ct)
    {
        // 如果之前在挣扎，现在恢复了
        if (_isStruggling)
        {
            _isStruggling = false;
            _struggleStartTime = null;
            _struggleCts?.Cancel();
            RecordState("从挣扎状态恢复", HostedServiceState.Running);
        }

        // 确保注册状态是完成的
        if (Status != RegistrationStatus.Completed)
        {
            Status = RegistrationStatus.Completed;
            _registrationCompletionSource.TrySetResult(true);
        }

        RecordState("心跳成功", HostedServiceState.Running);

        // 检查或维护 Leader 状态
        await CheckOrMaintainLeaderAsync(ct);
    }

    /// <summary>
    /// 处理心跳失败的情况
    /// </summary>
    private async Task HandleFailedHeartbeatAsync(string? errorMessage, CancellationToken ct)
    {
        RecordState($"心跳失败: {errorMessage}", HostedServiceState.Degraded);

        // 如果是 Leader，进入挣扎模式
        if (leaderService.IsLeader && !_isStruggling)
        {
            await StartStruggleAsync(ct);
        }
    }

    /// <summary>
    /// 检查或维护 Leader 状态
    /// </summary>
    private async Task CheckOrMaintainLeaderAsync(CancellationToken ct)
    {
        if (!leaderService.IsLeader)
        {
            // 当前不是 Leader，检查是否需要竞争
            var leaderExists = await stateManager.LeaderExistsAsync(ct);
            if (!leaderExists)
            {
                await CompeteForLeaderAsync(ct);
            }
            else
            {
                RecordState("Leader 已存在，保持 Follower 状态", givenLogLevel: LogLevel.Debug);
            }
        }
        else
        {
            // 当前是 Leader，续约
            await RenewLeaderLeaseAsync(ct);
        }
    }

    /// <summary>
    /// 竞争成为 Leader
    /// </summary>
    private async Task CompeteForLeaderAsync(CancellationToken ct)
    {
        RecordState("尝试竞争 Leader", givenLogLevel: LogLevel.Debug);

        var (success, state, eTag) = await stateManager.TryBecomeLeaderAsync(ct);

        if (success && state != null && eTag != null)
        {
            leaderService.SetAsLeader(state.BecomeLeaderTime, eTag);
            RecordState("成功成为 Leader", HostedServiceState.Running);
        }
        else
        {
            RecordState("Leader 竞争失败，其他实例可能已成为 Leader", givenLogLevel: LogLevel.Debug);
        }
    }

    /// <summary>
    /// 续约 Leader 租约
    /// </summary>
    private async Task RenewLeaderLeaseAsync(CancellationToken ct)
    {
        var currentETag = leaderService.CurrentETag;
        if (string.IsNullOrEmpty(currentETag))
        {
            RecordState("无法续约 Leader：ETag 为空", givenLogLevel: LogLevel.Warning);
            leaderService.TriggerLeaderLost(LeaderLostReason.NetworkIsolation);
            return;
        }

        var (success, newETag, actualState, actualETag) = await stateManager.RenewLeaderLeaseAsync(currentETag, ct);

        if (success && !string.IsNullOrEmpty(newETag))
        {
            leaderService.UpdateETag(newETag);
            RecordState("Leader 续约成功", givenLogLevel: LogLevel.Debug);
        }
        else
        {
            var currentInstanceId = clientInfo.GetServiceStatus().InstanceId;

            if (actualState != null && actualState.InstanceId != currentInstanceId)
            {
                // 其他实例已成为 Leader
                leaderService.TriggerLeaderLost(LeaderLostReason.LeaderKeyTakenByOther);
                RecordState($"Leader 被其他实例 ({actualState.InstanceId}) 抢占", HostedServiceState.Running);
            }
            else if (actualState != null && actualState.InstanceId == currentInstanceId && !string.IsNullOrEmpty(actualETag))
            {
                // 关键修复：我们仍是 Leader，但 ETag 过期了 → 刷新 ETag
                RecordState($"ETag 不一致但仍为 Leader，刷新 ETag: {currentETag} -> {actualETag}", givenLogLevel: LogLevel.Information);
                leaderService.UpdateETag(actualETag);
                // 不进入挣扎模式，下次心跳时用新 ETag 续约即可
            }
            else if (!_isStruggling)
            {
                // Leader key 不存在或获取失败，进入挣扎模式
                RecordState("Leader 续约失败且无法获取当前状态", HostedServiceState.Degraded, givenLogLevel: LogLevel.Warning);
                await StartStruggleAsync(ct);
            }
        }
    }

    /// <summary>
    /// 开始挣扎模式
    /// </summary>
    private async Task StartStruggleAsync(CancellationToken ct)
    {
        if (_isStruggling) return;

        _isStruggling = true;
        _struggleStartTime = DateTime.UtcNow;
        _struggleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        RecordState("进入挣扎模式", HostedServiceState.Degraded);

        // 启动挣扎循环
        _ = RunStruggleLoopAsync(_struggleCts.Token);

        await Task.CompletedTask;
    }

    /// <summary>
    /// 挣扎循环
    /// </summary>
    private async Task RunStruggleLoopAsync(CancellationToken ct)
    {
        while (_isStruggling && !ct.IsCancellationRequested)
        {
            try
            {
                // 检查是否超过放弃挣扎阈值
                var struggleDuration = DateTime.UtcNow - _struggleStartTime;
                if (struggleDuration >= _option.Election.GiveUpStruggleThreshold)
                {
                    if (leaderService.IsLeader)
                    {
                        leaderService.TriggerLeaderLost(LeaderLostReason.StruggleTimeout);
                    }

                    _isStruggling = false;
                    RecordState("挣扎超时，放弃 Leader", HostedServiceState.Running);

                    // 根据隔离处理模式决定后续行为
                    if (_option.IsolationHandlingMode == EIsolationHandlingMode.FastShutdown)
                    {
                        RecordState("隔离处理模式为 FastShutdown，触发服务隔离事件", givenLogLevel: LogLevel.Warning);
                        // 可以在这里触发更多的隔离处理逻辑
                    }

                    break;
                }

                // 尝试恢复心跳
                var result = await stateManager.RegisterOrHeartbeatAsync(ct);
                if (result.Success)
                {
                    _isStruggling = false;
                    _struggleStartTime = null;
                    RecordState("挣扎恢复成功", HostedServiceState.Running);

                    // 如果是 Leader，尝试续约
                    if (leaderService.IsLeader)
                    {
                        await RenewLeaderLeaseAsync(ct);
                    }

                    break;
                }

                RecordState($"挣扎中，心跳仍然失败，等待 {_option.Election.StrugglePeriod} 后重试", givenLogLevel: LogLevel.Debug);
                await Task.Delay(_option.Election.StrugglePeriod, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                RecordState("挣扎循环异常", exception: ex, givenLogLevel: LogLevel.Error);
            }
        }
    }

    public override void Dispose()
    {
        _struggleCts?.Cancel();
        _struggleCts?.Dispose();
        base.Dispose();
    }
}
