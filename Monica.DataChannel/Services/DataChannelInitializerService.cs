using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.DataChannel.Abstractions;
using Monica.Modules;

namespace Monica.DataChannel.Services;

/// <summary>
/// Initializes registered data channels during application startup.
/// </summary>
/// <remarks>
/// Creates a new <see cref="DataChannelInitializerService"/> instance.
/// </remarks>
/// <param name="manager">Manager that exposes registered data channels.</param>
/// <param name="options">Provides the data channel module options.</param>
/// <param name="observableManager">Registers the hosted service with observable-instance tracking.</param>
/// <param name="hostedServiceOptions">Provides shared hosted-service observability options.</param>
/// <param name="logger">The host logger used by the hosted-service observability base.</param>
public class DataChannelInitializerService(
    IDataChannelManager manager,
    IOptions<ModuleDataChannelOption> options,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    ILogger<DataChannelInitializerService> logger) : MoHostedService(observableManager, hostedServiceOptions, logger)
{
    private readonly int _initThreadCount = options.Value.InitThreadCount;

    /// <summary>
    /// Gets the service name shown in hosted-service observability.
    /// </summary>
    public override string ServiceName => nameof(DataChannelInitializerService);

    /// <summary>
    /// Gets the observable group for data-channel hosted services.
    /// </summary>
    public override string? ServiceGroupId => nameof(BuiltInModuleKey.DataChannel);

    /// <inheritdoc />
    protected override async Task OnStartingAsync(CancellationToken cancellationToken)
    {
        var channels = manager.FetchAll();
        var failureCount = 0;
        RecordState($"Initializing {channels.Count} data channel(s).", HostedServiceState.Executing);

        await Parallel.ForEachAsync(
            channels,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _initThreadCount,
                CancellationToken = cancellationToken
            },
            async (channel, token) =>
        {
            try
            {
                await channel.Pipe.InitAsync(token);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failureCount);
                RecordState($"Failed to initialize channel: {channel.Id}", HostedServiceState.Degraded, ex);
            }
        });

        if (failureCount > 0)
        {
            RecordState($"Data channel initialization completed with {failureCount} failure(s).", HostedServiceState.Degraded);
            return;
        }

        RecordState("Data channel initialization completed.", HostedServiceState.Running);
    }

    /// <inheritdoc />
    protected override async Task OnStoppingAsync(CancellationToken cancellationToken)
    {
        foreach (var channel in manager.FetchAll())
        {
            try
            {
                await channel.Pipe.DisposeAsync();
            }
            catch (Exception ex)
            {
                RecordState(
                    $"Failed to dispose channel: {channel.Id}",
                    HostedServiceState.Degraded,
                    ex);
            }
        }
    }
}
