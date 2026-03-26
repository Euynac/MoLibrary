using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Events;
using Monica.ServiceDiscovery.Models;

namespace Monica.ServiceDiscovery.Services.Support;

/// <summary>
/// Leader 选举服务实现
/// </summary>
public class LeaderElectionService(
    IServiceDiscoveryClientInfo clientInfo,
    IOptions<ModuleServiceDiscoveryOption> options,
    ILogger<LeaderElectionService> logger) : ILeaderElectionService
{
    private readonly ModuleServiceDiscoveryOption _option = options.Value;
    private readonly object _lock = new();

    private LeaderStatus _currentStatus = LeaderStatus.Looking;
    private DateTime? _leaderBecomeTime;
    private string? _currentETag;

    public LeaderStatus CurrentStatus
    {
        get { lock (_lock) { return _currentStatus; } }
    }

    public bool IsLeader
    {
        get { lock (_lock) { return _currentStatus == LeaderStatus.Leader; } }
    }

    public DateTime? LeaderBecomeTime
    {
        get { lock (_lock) { return _leaderBecomeTime; } }
    }

    public string? CurrentETag
    {
        get { lock (_lock) { return _currentETag; } }
    }

    public event EventHandler<LeaderGainedEvent>? OnLeaderGained;
    public event EventHandler<LeaderLostEvent>? OnLeaderLost;

    public void SetAsLeader(DateTime becomeTime, string eTag)
    {
        lock (_lock)
        {
            if (_currentStatus == LeaderStatus.Leader)
            {
                // 已经是 Leader，只更新 ETag
                _currentETag = eTag;
                return;
            }

            _currentStatus = LeaderStatus.Leader;
            _leaderBecomeTime = becomeTime;
            _currentETag = eTag;
        }

        logger.LogInformation("成为 Leader，时间: {Time}", becomeTime);

        // 触发事件
        var serviceStatus = clientInfo.GetServiceStatus();
        OnLeaderGained?.Invoke(this, new LeaderGainedEvent
        {
            ServiceName = serviceStatus.ServiceName,
            InstanceId = serviceStatus.InstanceId,
            BecomeLeaderTime = becomeTime
        });
    }

    public void UpdateETag(string newETag)
    {
        lock (_lock)
        {
            _currentETag = newETag;
        }
        logger.LogDebug("更新 ETag: {ETag}", newETag);
    }

    public void TriggerLeaderLost(LeaderLostReason reason)
    {
        DateTime lostTime;
        lock (_lock)
        {
            if (_currentStatus != LeaderStatus.Leader)
            {
                return;
            }

            _currentStatus = LeaderStatus.Follower;
            lostTime = DateTime.UtcNow;
            _leaderBecomeTime = null;
            _currentETag = null;
        }

        logger.LogWarning("失去 Leader 地位，原因: {Reason}", reason);

        // 触发事件
        var serviceStatus = clientInfo.GetServiceStatus();
        OnLeaderLost?.Invoke(this, new LeaderLostEvent
        {
            ServiceName = serviceStatus.ServiceName,
            InstanceId = serviceStatus.InstanceId,
            LostTime = lostTime,
            Reason = reason
        });
    }

    public void ResetLeaderState()
    {
        lock (_lock)
        {
            _currentStatus = LeaderStatus.Looking;
            _leaderBecomeTime = null;
            _currentETag = null;
        }
        logger.LogDebug("重置 Leader 状态");
    }
}
