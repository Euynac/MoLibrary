namespace BuildingBlocksPlatform.BackgroundWorker.Abstract.Workers;

public class MoBackgroundWorkerManager(
    IMoDashboardBackgroundWorkerManager dashboardManager,
    IMoSimpleBackgroundWorkerManager simpleManager)
    : IMoBackgroundWorkerManager, IDisposable
{
    public void Dispose()
    {
    }

    public async Task AddToDashboardAsync(Type workerType, string? queue = null,
        CancellationToken cancellationToken = default)
    {
        await dashboardManager.AddToDashboardAsync(workerType, queue, cancellationToken);
    }

    public async Task AddAsync(Type workerType, CancellationToken cancellationToken = default)
    {
        await simpleManager.AddAsync(workerType, cancellationToken);
    }
}