namespace Monica.StateStore.ProgressBar;

/// <summary>
/// Progress bar update event parameters
/// </summary>
public class ProgressBarEventArgs(ProgressBar status) : EventArgs
{
    public ProgressBar Status { get; } = status;
}

/// <summary>
/// Progress bar cancellation event parameters
/// </summary>
public class ProgressBarCancelledEventArgs(ProgressBar status, string? reason = null) : EventArgs
{
    public ProgressBar Status { get; } = status;
    public string? Reason { get; } = reason;
}