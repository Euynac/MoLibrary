using MoLibrary.DependencyInjection.AppInterfaces;

namespace MoLibrary.BackgroundJob.Abstract.Jobs;

public class JobExecutionContext(
    IServiceProvider serviceProvider,
    Type jobType,
    object jobArgs,
    CancellationToken cancellationToken = default)
    : IMoServiceProviderAccessor
{
    public CancellationToken CancellationToken { get; } = cancellationToken;

    public object JobArgs { get; } = jobArgs;

    public Type JobType { get; } = jobType;
    public IServiceProvider ServiceProvider { get; } = serviceProvider;
}