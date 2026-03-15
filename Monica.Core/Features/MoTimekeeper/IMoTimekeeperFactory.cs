namespace Monica.Core.Features.MoTimekeeper;

public interface IMoTimekeeperFactory
{
    /// <summary>
    /// Creates a timer that finishes automatically when disposed.
    /// </summary>
    /// <param name="key">The timer key.</param>
    /// <param name="content">Optional log content.</param>
    /// <returns>A disposable auto-finishing timekeeper.</returns>
    public AutoTimekeeper CreateAutoTimer(string key, string? content = null);

    /// <summary>
    /// Creates a timer that can be started and finished manually.
    /// </summary>
    /// <param name="key">The timer key.</param>
    /// <returns>A manually controlled timekeeper.</returns>
    public NormalTimekeeper CreateNormalTimer(string key);
}
