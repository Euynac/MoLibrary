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
/// <param name="logger">Logs initialization lifecycle events.</param>
public class DataChannelInitializerService(
    IDataChannelManager manager,
    IOptions<ModuleDataChannelOption> options,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    ILogger<DataChannelInitializerService>? logger = null) : MoHostedService(observableManager, hostedServiceOptions)
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
                logger?.LogInformation("Successfully initialized channel: {ChannelId}", channel.Id);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failureCount);
                RecordState($"Failed to initialize channel: {channel.Id}", HostedServiceState.Degraded, ex);
                logger?.LogError(ex, "Failed to initialize channel: {ChannelId}", channel.Id);
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
    protected override Task OnStoppingAsync(CancellationToken cancellationToken)
    {
        logger?.LogInformation("DataChannelInitializerService is stopping");
        return Task.CompletedTask;
    }
} 
