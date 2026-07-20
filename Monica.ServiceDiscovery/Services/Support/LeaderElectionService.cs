using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Events;
using Monica.ServiceDiscovery.Models;

namespace Monica.ServiceDiscovery.Services.Support;

/// <summary>
/// Leader election service implementation
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
                // Already the Leader, only update the ETag
                _currentETag = eTag;
                return;
            }

            _currentStatus = LeaderStatus.Leader;
            _leaderBecomeTime = becomeTime;
            _currentETag = eTag;
        }

        logger.LogInformation("Leadership acquired at {BecomeTime}.", becomeTime);

        // trigger event
        var serviceStatus = clientInfo.GetServiceStatus();
        OnLeaderGained?.Invoke(this, new LeaderGainedEvent
        {
            AppId = serviceStatus.AppId,
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
        logger.LogDebug("Leader ETag updated to {ETag}.", newETag);
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

        if (reason == LeaderLostReason.GracefulShutdown)
        {
            logger.LogInformation("Leadership released. Reason: {Reason}.", reason);
        }
        else
        {
            logger.LogWarning("Leadership lost. Reason: {Reason}.", reason);
        }

        // trigger event
        var serviceStatus = clientInfo.GetServiceStatus();
        OnLeaderLost?.Invoke(this, new LeaderLostEvent
        {
            AppId = serviceStatus.AppId,
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
        logger.LogDebug("Leader state reset.");
    }
}
