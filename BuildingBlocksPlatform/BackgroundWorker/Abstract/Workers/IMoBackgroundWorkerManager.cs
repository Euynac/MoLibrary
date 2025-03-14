namespace BuildingBlocksPlatform.BackgroundWorker.Abstract.Workers;

/// <summary>
///     Used to manage background workers.
/// </summary>
public interface IMoDashboardBackgroundWorkerManager
{
    public Task TriggerDashboardJobOnce(Type workerType);
    public Task AddToDashboardAsync(Type workerType, string? queue = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Used to manage background workers.
/// </summary>
public interface IMoSimpleBackgroundWorkerManager
{
    public Task AddAsync(Type workerType, CancellationToken cancellationToken = default);
}

/// <summary>
///     Used to manage background workers.
/// </summary>
public interface IMoBackgroundWorkerManager : IMoDashboardBackgroundWorkerManager, IMoSimpleBackgroundWorkerManager
{
}