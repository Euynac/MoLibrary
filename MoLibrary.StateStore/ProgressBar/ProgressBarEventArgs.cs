namespace MoLibrary.StateStore.ProgressBar;

/// <summary>
/// 进度条更新事件参数
/// </summary>
public class ProgressBarEventArgs(ProgressBar status) : EventArgs
{
    public ProgressBar Status { get; } = status;
}

/// <summary>
/// 进度条取消事件参数
/// </summary>
public class ProgressBarCancelledEventArgs(ProgressBar status, string? reason = null) : EventArgs
{
    public ProgressBar Status { get; } = status;
    public string? Reason { get; } = reason;
}