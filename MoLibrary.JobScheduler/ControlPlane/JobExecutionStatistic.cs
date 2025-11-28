namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Represents a running job instance for concurrency tracking
/// </summary>
public class RunningJobInfo
{
    public required string InstanceId { get; init; }
    public required string WorkerClientId { get; init; }
    public required DateTime StartedAt { get; init; }
}

/// <summary>
/// Tracks execution statistics for a job definition to enforce concurrency limits
/// </summary>
public class JobExecutionStatistic
{
    public required string JobKey { get; init; }
    public required int MaxConcurrency { get; init; }
    public List<RunningJobInfo> RunningInstances { get; init; } = [];

    /// <summary>
    /// Tracks reserved slots (instanceId → reservation time) for jobs that passed concurrency check but haven't started yet
    /// </summary>
    public Dictionary<string, DateTime> PendingReservations { get; init; } = new();

    /// <summary>
    /// Gets the current number of executing instances (running + pending reservations)
    /// </summary>
    public int CurrentExecutingCount => RunningInstances.Count + PendingReservations.Count;

    /// <summary>
    /// Checks if a new job can be executed without exceeding concurrency limit
    /// </summary>
    public bool CanExecute => CurrentExecutingCount < MaxConcurrency;

    /// <summary>
    /// Reserves a slot for a job instance (called during concurrency check)
    /// </summary>
    public void ReserveSlot(string instanceId)
    {
        PendingReservations[instanceId] = DateTime.UtcNow;
    }

    /// <summary>
    /// Confirms a reservation by moving it from pending to running (called when JobStartedEvent is received)
    /// </summary>
    public void ConfirmReservation(string instanceId, RunningJobInfo info)
    {
        PendingReservations.Remove(instanceId);

        // Prevent duplicate additions (handle event bus re-delivery)
        if (!RunningInstances.Any(i => i.InstanceId == instanceId))
        {
            RunningInstances.Add(info);
        }
    }

    /// <summary>
    /// Releases a reserved slot (called when event publishing fails or timeout occurs)
    /// </summary>
    public bool ReleaseReservation(string instanceId)
    {
        return PendingReservations.Remove(instanceId);
    }

    /// <summary>
    /// Adds a running instance to the tracking list (legacy method for backward compatibility)
    /// </summary>
    public void AddInstance(RunningJobInfo info)
    {
        RunningInstances.Add(info);
    }

    /// <summary>
    /// Removes a running instance from the tracking list
    /// </summary>
    public bool RemoveInstance(string instanceId)
    {
        return RunningInstances.RemoveAll(i => i.InstanceId == instanceId) > 0;
    }

    /// <summary>
    /// Removes all instances running on a specific worker client
    /// </summary>
    public List<RunningJobInfo> RemoveInstancesByWorker(string workerClientId)
    {
        var removed = RunningInstances.Where(i => i.WorkerClientId == workerClientId).ToList();
        RunningInstances.RemoveAll(i => i.WorkerClientId == workerClientId);
        return removed;
    }
}
