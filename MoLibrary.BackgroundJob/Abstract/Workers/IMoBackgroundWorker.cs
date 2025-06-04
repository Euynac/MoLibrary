namespace MoLibrary.BackgroundJob.Abstract.Workers;

/// <summary>
///     Interface for a worker (thread) that runs on background to perform some tasks.
/// </summary>
public interface IMoBackgroundWorker
{
}

public interface IMoDashboardBackgroundWorker : IMoBackgroundWorker
{
}

public interface IMoSimpleBackgroundWorker : IMoBackgroundWorker
{
}