using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoLibrary.StateStore.CancellationManager;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.StateStore.Modules;

namespace MoLibrary.StateStore.ProgressBar;

/// <summary>
/// 进度条服务实现
/// </summary>
public class MoProgressBarService : BackgroundService, IMoProgressBarService
{
    private readonly IMoStateStore _stateStore;
    private readonly IMoCancellationManager _cancellationManager;
    private readonly ILogger<MoProgressBarService> _logger;
    private readonly ConcurrentDictionary<string, ProgressBarAutoUpdateInfo> _autoUpdateTasks = new();
    private readonly Timer _cleanupTimer;

    public MoProgressBarService(
        [FromKeyedServices(nameof(ModuleProgressBar))] IMoStateStore stateStore,
        [FromKeyedServices(nameof(ModuleProgressBar))] IMoCancellationManager cancellationManager,
        ILogger<MoProgressBarService> logger)
    {
        _stateStore = stateStore;
        _cancellationManager = cancellationManager;
        _logger = logger;
        
        // 创建清理定时器，每分钟执行一次清理
        _cleanupTimer = new Timer(CleanupExpiredProgressBars, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// 创建进度条任务
    /// </summary>
    public async Task<ProgressBar> CreateProgressBarAsync(string? id = null, Action<ProgressBarSetting>? settingAction = null)
    {
        return await CreateProgressBarAsync<ProgressBar>(id, settingAction);
    }

    /// <summary>
    /// 创建自定义进度条任务
    /// </summary>
    public async Task<TCustom> CreateProgressBarAsync<TCustom>(string? id = null, Action<ProgressBarSetting>? settingAction = null)
        where TCustom : ProgressBar
    {
        var taskId = id ?? $"ProgressBar_{Guid.NewGuid().ToString()}";
        var setting = new ProgressBarSetting();
        settingAction?.Invoke(setting);

        // 创建进度条实例
        var progressBar = (TCustom)Activator.CreateInstance(typeof(TCustom), setting, this, taskId)!;

        // 获取或创建分布式取消令牌
        var cancellationToken = await _cancellationManager.GetOrCreateTokenAsync(taskId);
        progressBar.SetCancellationToken(cancellationToken);

        // 保存初始状态
        await SaveProgressBarStateAsync(progressBar);

        // 设置自动更新
        if (setting.AutoUpdateDuration.HasValue)
        {
            SetupAutoUpdate(progressBar, setting.AutoUpdateDuration.Value);
        }

        _logger.LogInformation("Created progress bar task: {TaskId}", taskId);
        return progressBar;
    }

    /// <summary>
    /// 获取进度条状态
    /// </summary>
    public async Task<ProgressBarStatus> GetProgressBarStatus(string id)
    {
        var status = await _stateStore.GetStateAsync<ProgressBarStatus>(id);
        return status ?? throw new InvalidOperationException($"Progress bar '{id}' not found.");
    }

    /// <summary>
    /// 保存进度条状态
    /// </summary>
    public async ValueTask SaveProgressBarStateAsync(ProgressBar progressBar)
    {
        try
        {
            await _stateStore.SaveStateAsync(progressBar.TaskId, progressBar.Status, ttl: progressBar.Setting.TimeToLive);
            _logger.LogDebug("Saved progress bar state: {TaskId}, Progress: {Progress}%", 
                progressBar.TaskId, progressBar.Status.Percentage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save progress bar state: {TaskId}", progressBar.TaskId);
            throw;
        }
    }

    /// <summary>
    /// 完成进度条任务
    /// </summary>
    public async Task FinishProgressBarAsync(ProgressBar progressBar)
    {
        try
        {
            // 停止自动更新
            StopAutoUpdate(progressBar.TaskId);

            // 保存最终状态，使用完成后的TTL
            await _stateStore.SaveStateAsync(progressBar.TaskId, progressBar.Status, ttl: progressBar.Setting.CompletedTimeToLive);
            
            _logger.LogInformation("Finished progress bar task: {TaskId}", progressBar.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to finish progress bar: {TaskId}", progressBar.TaskId);
            throw;
        }
    }

    /// <summary>
    /// 取消进度条任务
    /// </summary>
    public async Task CancelProgressBarAsync(ProgressBar progressBar, string? reason = null)
    {
        try
        {
            // 停止自动更新
            StopAutoUpdate(progressBar.TaskId);

            // 通过取消管理器发送取消信号
            await _cancellationManager.CancelTokenAsync(progressBar.TaskId);

            // 保存取消状态，使用完成后的TTL
            await _stateStore.SaveStateAsync(progressBar.TaskId, progressBar.Status, ttl: progressBar.Setting.CompletedTimeToLive);
            
            _logger.LogInformation("Cancelled progress bar task: {TaskId}, Reason: {Reason}", progressBar.TaskId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel progress bar: {TaskId}", progressBar.TaskId);
            throw;
        }
    }

    /// <summary>
    /// 设置自动更新
    /// </summary>
    private void SetupAutoUpdate(ProgressBar progressBar, TimeSpan interval)
    {
        var info = new ProgressBarAutoUpdateInfo
        {
            ProgressBar = progressBar,
            Timer = new Timer(async _ => await AutoUpdateCallback(progressBar), null, interval, interval)
        };
        
        _autoUpdateTasks.TryAdd(progressBar.TaskId, info);
    }

    /// <summary>
    /// 停止自动更新
    /// </summary>
    private void StopAutoUpdate(string taskId)
    {
        if (_autoUpdateTasks.TryRemove(taskId, out var info))
        {
            info.Timer?.Dispose();
        }
    }

    /// <summary>
    /// 自动更新回调
    /// </summary>
    private async Task AutoUpdateCallback(ProgressBar progressBar)
    {
        if (progressBar.IsCompleted || progressBar.IsCancelled)
        {
            StopAutoUpdate(progressBar.TaskId);
            return;
        }

        try
        {
            await SaveProgressBarStateAsync(progressBar);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto update failed for progress bar: {TaskId}", progressBar.TaskId);
        }
    }

    /// <summary>
    /// 清理过期的进度条
    /// </summary>
    private async void CleanupExpiredProgressBars(object? state)
    {
        try
        {
            // 这里可以实现清理逻辑，比如删除过期的状态
            // 由于IMoStateStore接口中的TTL机制应该会自动处理过期数据，
            // 这里主要是清理内存中的自动更新任务
            
            var expiredTasks = _autoUpdateTasks.Where(kvp => 
                kvp.Value.ProgressBar.IsCompleted || kvp.Value.ProgressBar.IsCancelled).ToList();

            foreach (var expiredTask in expiredTasks)
            {
                StopAutoUpdate(expiredTask.Key);
                _logger.LogDebug("Cleaned up expired progress bar: {TaskId}", expiredTask.Key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during progress bar cleanup");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 后台服务主循环，可以用于监控和维护
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                // 这里可以添加定期维护逻辑
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in progress bar service background task");
            }
        }
    }

    public override void Dispose()
    {
        _cleanupTimer?.Dispose();
        
        // 停止所有自动更新任务
        foreach (var info in _autoUpdateTasks.Values)
        {
            info.Timer?.Dispose();
        }
        _autoUpdateTasks.Clear();
        
        base.Dispose();
    }
}

/// <summary>
/// 自动更新信息
/// </summary>
internal class ProgressBarAutoUpdateInfo
{
    public required ProgressBar ProgressBar { get; set; }
    public Timer? Timer { get; set; }
} 