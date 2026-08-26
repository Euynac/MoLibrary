namespace Monica.JobScheduler.Services.Support;

/// <summary>
/// Holds the local host's readiness facts without duplicating durable scheduler state.
/// </summary>
internal sealed class JobSchedulerRuntimeState
{
    private int _schedulingReady;
    private int _workerReady;
    private string? _schedulingMessage;
    private string? _workerMessage;

    internal bool SchedulingReady => Volatile.Read(ref _schedulingReady) != 0;
    internal bool WorkerReady => Volatile.Read(ref _workerReady) != 0;
    internal string? SchedulingMessage => Volatile.Read(ref _schedulingMessage);
    internal string? WorkerMessage => Volatile.Read(ref _workerMessage);

    internal bool SetScheduling(bool ready, string message) =>
        SetState(ref _schedulingReady, ref _schedulingMessage, ready, message);

    internal bool SetWorker(bool ready, string message) =>
        SetState(ref _workerReady, ref _workerMessage, ready, message);

    private static bool SetState(ref int state, ref string? currentMessage, bool ready, string message)
    {
        Volatile.Write(ref currentMessage, message);
        var value = ready ? 1 : 0;
        return Interlocked.Exchange(ref state, value) != value;
    }
}
