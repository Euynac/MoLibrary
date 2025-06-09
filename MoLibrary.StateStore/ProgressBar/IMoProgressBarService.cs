namespace MoLibrary.StateStore.ProgressBar;

public interface IMoProgressBarService
{
    /// <summary>
    /// 创建一个新的进度条任务
    /// </summary>
    /// <param name="id">为空则生成GUID作为Key</param>
    /// <param name="settingAction"></param>
    /// <returns></returns>
    Task<ProgressBar> CreateProgressBarAsync(string? id = null, Action<ProgressBarSetting>? settingAction = null);

    /// <summary>
    /// 创建一个新的自定义进度条任务
    /// </summary>
    /// <typeparam name="TCustom"></typeparam>
    /// <param name="id">为空则生成GUID作为Key</param>
    /// <param name="settingAction"></param>
    /// <returns></returns>
    Task<TCustom> CreateProgressBarAsync<TCustom>(string? id = null, Action<ProgressBarSetting>? settingAction = null)
        where TCustom : ProgressBar;

    /// <summary>
    /// 获取指定进度条状态
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<ProgressBarStatus> GetProgressBarStatus(string id);


    /// <summary>
    /// 更新进度条状态
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    ValueTask SaveProgressBarStateAsync(ProgressBar status);


    /// <summary>
    /// 完成进度条任务
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    Task FinishProgressBarAsync(ProgressBar status);

    /// <summary>
    /// 取消进度条任务
    /// </summary>
    /// <param name="status"></param>
    /// <param name="reason">取消原因</param>
    /// <returns></returns>
    Task CancelProgressBarAsync(ProgressBar status, string? reason = null);
}

public class ProgressBarSetting
{
    /// <summary>
    /// 默认为空，当进度条每次进度更新时候即时更新状态到状态存储。对于一些进度变更频繁的进度条，建议设置此自动更新的时间间隔，后台每隔设定的时间间隔会判断是否需要自动更新状态到状态存储。
    /// </summary>
    public TimeSpan? AutoUpdateDuration { get; set; }

    /// <summary>
    /// 进度条任务的总步数，默认为100
    /// </summary>
    public int TotalSteps { get; set; } = 100;

    /// <summary>
    /// 进度条状态的生存时间，超过此时间后状态未更新将被自动清理，默认为10分钟
    /// </summary>
    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 进度条状态完成后的生存时间，超过此时间后状态将被自动清理，默认为3分钟
    /// </summary>
    public TimeSpan CompletedTimeToLive { get; set; } = TimeSpan.FromMinutes(3);
}