namespace MoLibrary.StateStore.ProgressBar;

/// <summary>
/// 进度条更新事件参数
/// </summary>
public class ProgressBarEventArgs(ProgressBar status) : EventArgs
{
    public ProgressBar Status { get; } = status;
}