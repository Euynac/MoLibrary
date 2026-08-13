namespace Monica.JobScheduler.Services.Support;

/// <summary>
/// Holds the local host's readiness facts without duplicating durable scheduler state.
/// </summary>
internal sealed class JobSchedulerRuntimeState
{
    private int _controlPlaneReady;
    private int _workerReady;
    private string? _controlPlaneMessage;
    private string? _workerMessage;

    internal bool ControlPlaneReady => Volatile.Read(ref _controlPlaneReady) != 0;
    internal bool WorkerReady => Volatile.Read(ref _workerReady) != 0;
    internal string? ControlPlaneMessage => Volatile.Read(ref _controlPlaneMessage);
    internal string? WorkerMessage => Volatile.Read(ref _workerMessage);

    internal bool SetControlPlane(bool ready, string message) =>
        SetState(ref _controlPlaneReady, ref _controlPlaneMessage, ready, message);

    internal bool SetWorker(bool ready, string message) =>
        SetState(ref _workerReady, ref _workerMessage, ready, message);

    private static bool SetState(ref int state, ref string? currentMessage, bool ready, string message)
    {
        Volatile.Write(ref currentMessage, message);
        var value = ready ? 1 : 0;
        return Interlocked.Exchange(ref state, value) != value;
    }
}
