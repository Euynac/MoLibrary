using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Modules;

namespace Monica.DataChannel.Services;

/// <summary>
/// Initializes registered data channels during application startup.
/// </summary>
/// <remarks>
/// Creates a new <see cref="DataChannelInitializerService"/> instance.
/// </remarks>
/// <param name="options">Provides the data channel module options.</param>
/// <param name="logger">Logs initialization lifecycle events.</param>
public class DataChannelInitializerService(IOptions<ModuleDataChannelOption> options, ILogger<DataChannelInitializerService>? logger = null) : IHostedService
{
    private readonly int _initThreadCount = options.Value.InitThreadCount;

    /// <summary>
    /// Starts the channel initialization process.
    /// </summary>
    /// <param name="cancellationToken">Cancels the initialization loop.</param>
    /// <returns>A completed task once the background initialization work has been queued.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Parallel.ForEachAsync(DataChannelCentral.Channels,new ParallelOptions{MaxDegreeOfParallelism = _initThreadCount,CancellationToken = cancellationToken}, async (channel, token) =>
        {
            try
            {
                await channel.Value.Pipe.InitAsync(token);
                logger?.LogInformation("Successfully initialized channel: {ChannelId}", channel.Key);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to initialize channel: {ChannelId}", channel.Key);
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the initializer service.
    /// </summary>
    /// <param name="cancellationToken">Cancels the stop operation.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger?.LogInformation("DataChannelInitializerService is stopping");
        return Task.CompletedTask;
    }
} 
