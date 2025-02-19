using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;

namespace BuildingBlocksPlatform.BackgroundWorker.Abstract.Jobs;

public class JobExecutionContext : IMoServiceProviderAccessor
{
    public JobExecutionContext(
        IServiceProvider serviceProvider,
        Type jobType,
        object jobArgs,
        CancellationToken cancellationToken = default)
    {
        ServiceProvider = serviceProvider;
        JobType = jobType;
        JobArgs = jobArgs;
        CancellationToken = cancellationToken;
    }

    public CancellationToken CancellationToken { get; }

    public object JobArgs { get; }

    public Type JobType { get; }
    public IServiceProvider ServiceProvider { get; }
}