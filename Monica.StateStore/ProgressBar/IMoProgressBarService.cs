namespace Monica.StateStore.ProgressBar;

/// <summary>
/// (Singleton) Progress bar service interface
/// </summary>
public interface IMoProgressBarService
{
    /// <summary>
    /// Create a new progress bar task
    /// </summary>
    /// <param name="id">If it is empty, a GUID is generated as the Key.</param>
    /// <param name="settingAction"></param>
    /// <returns></returns>
    Task<ProgressBar> CreateProgressBarAsync(string? id = null, Action<ProgressBarSetting>? settingAction = null);

    /// <summary>
    /// Get distributed progress bar
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<ProgressBar?> FetchDistributedProgressBar(string id);
    /// <summary>
    /// Create a new custom progress bar task
    /// </summary>
    /// <typeparam name="TCustomProgressBar"></typeparam>
    /// <param name="id">If it is empty, a GUID is generated as the Key.</param>
    /// <param name="settingAction"></param>
    /// <returns></returns>
    Task<TCustomProgressBar> CreateProgressBarAsync<TCustomProgressBar>(string? id = null, Action<ProgressBarSetting>? settingAction = null)
        where TCustomProgressBar : ProgressBar;


    /// <summary>
    /// Get a custom distributed progress bar
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<TCustomProgressBar?> FetchDistributedProgressBar<TCustomProgressBar, TCustomStatus>(string id) where TCustomProgressBar : ProgressBar where TCustomStatus : ProgressBarStatus;
    /// <summary>
    /// Get the cancellation token of the specified progress bar
    /// </summary>
    /// <param name="id">Unique identification key for cancellation token</param>
    /// <returns>Returns the cancellation token associated with the specified key</returns>
    Task<CancellationToken> GetProgressBarCancellationTokenAsync(string id);
    /// <summary>
    /// Get the specified progress bar status
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<ProgressBarStatus?> GetProgressBarStatusAsync(string id);

    /// <summary>
    /// Get the custom status of the specified progress bar
    /// </summary>
    /// <typeparam name="TCustomStatus">Custom status type</typeparam>
    /// <param name="id">Progress bar ID</param>
    /// <returns></returns>
    Task<TCustomStatus?> GetProgressBarStatusAsync<TCustomStatus>(string id) where TCustomStatus : ProgressBarStatus;

    /// <summary>
    /// Update progress bar status
    /// </summary>
    /// <param name="progressBar">Progress bar example</param>
    /// <param name="saveInstantly">Whether to save immediately, default false. If the progress bar has auto-update settings and this parameter is false, it will not be saved immediately.</param>
    /// <param name="isComplete">Is it a completed state?</param>
    /// <returns></returns>
    ValueTask SaveProgressBarStateAsync(ProgressBar progressBar, bool saveInstantly = false,
        bool isComplete = false);

    /// <summary>
    /// Complete progress bar task
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    Task FinishProgressBarAsync(ProgressBar status);

    /// <summary>
    /// Cancel progress bar task
    /// </summary>
    /// <param name="status"></param>
    /// <param name="reason">Reason for cancellation</param>
    /// <returns></returns>
    Task CancelProgressBarAsync(ProgressBar status, string? reason = null);
}

public class ProgressBarSetting
{
    /// <summary>
    /// The default is empty. When the progress bar is updated every time, the status is updated to the status storage immediately. For some progress bars with frequent progress changes, it is recommended to set this automatic update time interval. The background will determine whether it needs to automatically update the status to the status storage every set time interval.
    /// </summary>
    public TimeSpan? AutoUpdateDuration { get; set; }

    /// <summary>
    /// Create a distributed progress bar
    /// </summary>
    public bool UseDistributedProgressBar { get; set; }

    /// <summary>
    /// The total number of steps in the progress bar task, the default is 100
    /// </summary>
    public int TotalSteps { get; set; } = 100;

    /// <summary>
    /// The survival time of the progress bar status. If the status is not updated after this time, it will be automatically cleared. The default is 5 minutes.
    /// </summary>
    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The survival time after the progress bar status is completed. After this time, the status will be automatically cleared. The default is 3 minutes.
    /// </summary>
    public TimeSpan CompletedTimeToLive { get; set; } = TimeSpan.FromMinutes(3);
    
    /// <summary>
    /// distributed imprint
    /// </summary>
    internal string? DistributedStamp { get; set; }
}