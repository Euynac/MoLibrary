using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.StateStore.CancellationManager;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.Core.Extensions;

namespace Monica.StateStore.ProgressBar;

/// <summary>
/// Progress bar service implementation
/// </summary>
public class MoProgressBarService(
    [FromKeyedServices(nameof(ModuleProgressBar))] IMoStateStore stateStore,
    [FromKeyedServices(nameof(ModuleProgressBar))]
    IMoCancellationManager cancellationManager,
    ILogger<MoProgressBarService> logger,
    IOptions<ModuleProgressBarOption> options)
    : BackgroundService, IMoProgressBarService
{
    private const string SETTING_PREFIX = "ProgressBar:Setting:";
    private const string STATUS_PREFIX = "ProgressBar:Status:";

    public ModuleProgressBarOption Options { get; } = options.Value;
    private readonly ConcurrentDictionary<string, ProgressBarAutoUpdateInfo> _autoUpdateTasks = new();

    public async Task<ProgressBar> CreateProgressBarAsync(string? id = null, Action<ProgressBarSetting>? settingAction = null)
    {
        return await CreateProgressBarAsync<ProgressBar>(id, settingAction);
    }

    public async Task<ProgressBar?> FetchDistributedProgressBar(string id)
    {
        return await FetchDistributedProgressBar<ProgressBar, ProgressBarStatus>(id);
    }

    public async Task<TCustomProgressBar?> FetchDistributedProgressBar<TCustomProgressBar, TCustomStatus>(string id)
        where TCustomProgressBar : ProgressBar
        where TCustomStatus : ProgressBarStatus
    {
        var setting = await stateStore.GetStateAsync<ProgressBarSetting>(SETTING_PREFIX + id);
        if (setting == null) return null;

        var status = await GetProgressBarStatusAsync<TCustomStatus>(id);

        // Create a progress bar instance
        var progressBar = (TCustomProgressBar)Activator.CreateInstance(typeof(TCustomProgressBar), setting, this, id)!;

        // Obtain or create a distributed cancellation token
        var cancellationToken = await cancellationManager.GetOrCreateTokenAsync(id);
        progressBar.SetCancellationToken(cancellationToken);
        progressBar.InitProgressBarStatus(status);
        progressBar.DistributedStamp = GenerateDistributedStamp();
        return progressBar;
    }

    /// <summary>
    /// Create a custom progress bar task
    /// </summary>
    public async Task<TCustom> CreateProgressBarAsync<TCustom>(string? id = null, Action<ProgressBarSetting>? settingAction = null)
        where TCustom : ProgressBar
    {
        var taskId = id ?? $"ProgressBar_{Guid.NewGuid()}";
        var setting = new ProgressBarSetting();
        settingAction?.Invoke(setting);

        try
        {
            // Create a progress bar instance
            var progressBar = (TCustom)Activator.CreateInstance(typeof(TCustom), setting, this, taskId)!;

            // Obtain or create a distributed cancellation token
            var cancellationToken = await cancellationManager.GetOrCreateTokenAsync(taskId);
            progressBar.SetCancellationToken(cancellationToken);
            progressBar.InitProgressBarStatus();

            if (setting.UseDistributedProgressBar)
            {
                progressBar.DistributedStamp = GenerateDistributedStamp();
            }

            // Save initial state
            await SaveProgressBarStateAsync(progressBar, saveInstantly: true);

            // Set up automatic updates
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

    public virtual string GenerateDistributedStamp()
    {
        return $"{Assembly.GetEntryAssembly()?.GetName()}-{Guid.NewGuid().ToString()}";
    }

    public async Task<CancellationToken> GetProgressBarCancellationTokenAsync(string id)
    {
        return await cancellationManager.GetOrCreateTokenAsync(id);
    }

    /// <summary>
    /// Get progress bar status
    /// </summary>
    public async Task<ProgressBarStatus?> GetProgressBarStatusAsync(string id)
    {
        try
        {
            var status = await stateStore.GetStateAsync<ProgressBarStatus>(STATUS_PREFIX + id);
            return status;
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to get progress bar status: {0}", id);
        }
    }

    /// <summary>
    /// Get the custom status of the specified progress bar
    /// </summary>
    /// <typeparam name="T">Custom status type</typeparam>
    /// <param name="id">Progress bar ID</param>
    /// <returns></returns>
    public async Task<T?> GetProgressBarStatusAsync<T>(string id) where T : ProgressBarStatus
    {
        try
        {
            var status = await stateStore.GetStateAsync<T>(STATUS_PREFIX + id);
            return status;
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to get custom progress bar status: {0}", id);
        }
    }

    public async ValueTask SaveProgressBarStateAsync(ProgressBar progressBar, bool saveInstantly = false,
        bool isComplete = false)
    {
        try
        {
            if (progressBar.Setting.UseDistributedProgressBar)
            {
                progressBar.Setting.DistributedStamp = progressBar.DistributedStamp;
                await stateStore.SaveStateAsync(SETTING_PREFIX + progressBar.TaskId, progressBar.Setting, ttl: progressBar.Setting.TimeToLive);
            }

            // Skip saving if auto-update is set and does not require immediate saving
            if (progressBar.Setting.AutoUpdateDuration.HasValue && !saveInstantly)
            {
                logger.LogTrace("Skipped saving progress bar state due to auto-update: {TaskId}", progressBar.TaskId);
                return;
            }

            await stateStore.SaveStateAsync(STATUS_PREFIX + progressBar.TaskId, progressBar.Status, ttl: isComplete ? progressBar.Setting.CompletedTimeToLive : progressBar.Setting.TimeToLive);

            logger.LogDebug("Saved progress bar state: {TaskId}, Progress: {Progress}%",
                progressBar.TaskId, progressBar.Status.Percentage);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to save progress bar state: {0}", progressBar.TaskId);
        }
    }

    /// <summary>
    /// Complete progress bar task
    /// </summary>
    public async Task FinishProgressBarAsync(ProgressBar progressBar)
    {
        try
        {
            // Stop automatic updates
            StopAutoUpdate(progressBar.TaskId);

            await SaveProgressBarStateAsync(progressBar, true, true);

            // Delete CancelToken via Cancel Manager
            await cancellationManager.DeleteTokenAsync(progressBar.TaskId);

            logger.LogInformation("Finished progress bar task: {TaskId}", progressBar.TaskId);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to finish progress bar: {0}", progressBar.TaskId);
        }
    }

    /// <summary>
    /// Cancel progress bar task
    /// </summary>
    public async Task CancelProgressBarAsync(ProgressBar progressBar, string? reason = null)
    {
        try
        {
            // Stop automatic updates
            StopAutoUpdate(progressBar.TaskId);

            // Send cancellation signal via cancellation manager
            await cancellationManager.CancelTokenAsync(progressBar.TaskId);

            await SaveProgressBarStateAsync(progressBar, true, true);

            logger.LogInformation("Cancelled progress bar task: {TaskId}, Reason: {Reason}", progressBar.TaskId, reason);
        }
        catch (Exception e)
        {
            throw e.CreateException(logger, "Failed to cancel progress bar: {0}, Reason: {1}", progressBar.TaskId, reason);
        }
    }

    /// <summary>
    /// Set up automatic updates
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
    /// Stop automatic updates
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
    /// Automatic update callback
    /// </summary>
    private async Task AutoUpdateCallback(ProgressBar progressBar)
    {
        // Stop automatic updates if task is completed or canceled
        if (progressBar.IsCompleted || progressBar.IsCancelled)
        {
            StopAutoUpdate(progressBar.TaskId);
            return;
        }

        if (!progressBar.IsCurrentTurn)
        {
            return;
        }

        try
        {
            // Force save during automatic update
            await SaveProgressBarStateAsync(progressBar, true);

            logger.LogTrace("Auto-updated progress bar state: {TaskId}, Progress: {Progress}%",
                progressBar.TaskId, progressBar.Status.Percentage);
        }
        catch (Exception e)
        {
            e.CreateException(logger, "Auto update failed for progress bar: {0}", progressBar.TaskId);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //// Background service main loop, which can be used for monitoring and maintenance
        //while (!stoppingToken.IsCancellationRequested)
        //{
        //    try
        //    {
        //        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        // //You can add regular maintenance logic here
        //    }
        //    catch (TaskCanceledException)
        //    {
        //        break;
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.LogError(ex, "Error in progress bar service background task");
        //    }
        //}
    }

    public override void Dispose()
    {
        // Stop all automatic update tasks
        foreach (var info in _autoUpdateTasks.Values)
        {
            info.Timer?.Dispose();
        }
        _autoUpdateTasks.Clear();

        base.Dispose();
    }
}

/// <summary>
/// Automatically update information
/// </summary>
internal class ProgressBarAutoUpdateInfo
{
    public required ProgressBar ProgressBar { get; set; }
    public Timer? Timer { get; set; }
}
