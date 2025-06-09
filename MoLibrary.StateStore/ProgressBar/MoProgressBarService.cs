using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoLibrary.StateStore.CancellationManager;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.StateStore.Modules;
using MoLibrary.Core.Extensions;

namespace MoLibrary.StateStore.ProgressBar;

/// <summary>
/// 进度条服务实现
/// </summary>
public class MoProgressBarService(
    [FromKeyedServices(nameof(ModuleProgressBar))] IMoStateStore stateStore,
    [FromKeyedServices(nameof(ModuleProgressBar))]
    IMoCancellationManager cancellationManager,
    ILogger<MoProgressBarService> logger)
    : BackgroundService, IMoProgressBarService
{
    private readonly ConcurrentDictionary<string, ProgressBarAutoUpdateInfo> _autoUpdateTasks = new();

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
        var taskId = id ?? $"ProgressBar_{Guid.NewGuid()}";
        var setting = new ProgressBarSetting();
        settingAction?.Invoke(setting);

        try
        {
            // 创建进度条实例
            var progressBar = (TCustom)Activator.CreateInstance(typeof(TCustom), setting, this, taskId)!;

            // 获取或创建分布式取消令牌
            var cancellationToken = await cancellationManager.GetOrCreateTokenAsync(taskId);
            progressBar.SetCancellationToken(cancellationToken);

            // 保存初始状态
            await SaveProgressBarStateAsync(progressBar, saveInstantly: true);

            // 设置自动更新
            if (setting.AutoUpdateDuration.HasValue)
            {
                SetupAutoUpdate(progressBar, setting.AutoUpdateDuration.Value);
            }

            logger.LogInformation("Created progress bar task: {TaskId}", taskId);
            return progressBar;
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to create progress bar task: {0}", taskId);
        }
    }

    /// <summary>
    /// 获取进度条状态
    /// </summary>
    public async Task<ProgressBarStatus> GetProgressBarStatus(string id)
    {
        try
        {
            var status = await stateStore.GetStateAsync<ProgressBarStatus>(id);
            return status ?? throw new InvalidOperationException($"Progress bar '{id}' not found.");
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to get progress bar status: {0}", id);
        }
    }

    /// <summary>
    /// 保存进度条状态
    /// </summary>
    /// <param name="progressBar">进度条实例</param>
    /// <param name="saveInstantly">是否立即保存，默认false。如果进度条有自动更新设置且此参数为false，则不会立即保存</param>
    public async ValueTask SaveProgressBarStateAsync(ProgressBar progressBar, bool saveInstantly = false)
    {
        try
        {
            // 如果设置了自动更新且不要求立即保存，则跳过保存
            if (progressBar.Setting.AutoUpdateDuration.HasValue && !saveInstantly)
            {
                logger.LogTrace("Skipped saving progress bar state due to auto-update: {TaskId}", progressBar.TaskId);
                return;
            }

            await stateStore.SaveStateAsync(progressBar.TaskId, progressBar.Status, ttl: progressBar.Setting.TimeToLive);
            logger.LogDebug("Saved progress bar state: {TaskId}, Progress: {Progress}%", 
                progressBar.TaskId, progressBar.Status.Percentage);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to save progress bar state: {0}", progressBar.TaskId);
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
            await stateStore.SaveStateAsync(progressBar.TaskId, progressBar.Status, ttl: progressBar.Setting.CompletedTimeToLive);
            
            logger.LogInformation("Finished progress bar task: {TaskId}", progressBar.TaskId);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to finish progress bar: {0}", progressBar.TaskId);
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
            await cancellationManager.CancelTokenAsync(progressBar.TaskId);

            // 保存取消状态，使用完成后的TTL
            await stateStore.SaveStateAsync(progressBar.TaskId, progressBar.Status, ttl: progressBar.Setting.CompletedTimeToLive);
            
            logger.LogInformation("Cancelled progress bar task: {TaskId}, Reason: {Reason}", progressBar.TaskId, reason);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to cancel progress bar: {0}, Reason: {1}", progressBar.TaskId, reason);
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
        logger.LogDebug("Setup auto-update for progress bar: {TaskId}, Interval: {Interval}", progressBar.TaskId, interval);
    }

    /// <summary>
    /// 停止自动更新
    /// </summary>
    private void StopAutoUpdate(string taskId)
    {
        if (_autoUpdateTasks.TryRemove(taskId, out var info))
        {
            info.Timer?.Dispose();
            logger.LogDebug("Stopped auto-update for progress bar: {TaskId}", taskId);
        }
    }

    /// <summary>
    /// 自动更新回调
    /// </summary>
    private async Task AutoUpdateCallback(ProgressBar progressBar)
    {
        // 如果任务已完成或取消，停止自动更新
        if (progressBar.IsCompleted || progressBar.IsCancelled)
        {
            StopAutoUpdate(progressBar.TaskId);
            return;
        }

        try
        {
            // 自动更新时强制保存
            await stateStore.SaveStateAsync(progressBar.TaskId, progressBar.Status, ttl: progressBar.Setting.TimeToLive);
            logger.LogTrace("Auto-updated progress bar state: {TaskId}, Progress: {Progress}%", 
                progressBar.TaskId, progressBar.Status.Percentage);
        }
        catch (Exception e)
        {
            e.CreateException(logger, "Auto update failed for progress bar: {0}", progressBar.TaskId)
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
                logger.LogError(ex, "Error in progress bar service background task");
            }
        }
    }

    public override void Dispose()
    {
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