using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MoLibrary.DataChannel.Services;

/// <summary>
/// Service responsible for initializing all data channels during application startup.
/// </summary>
public class DataChannelInitializerService : IHostedService
{
    private readonly ILogger<DataChannelInitializerService>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataChannelInitializerService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for logging initialization events.</param>
    public DataChannelInitializerService(ILogger<DataChannelInitializerService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Starts the initialization of all registered data channels.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Starting initialization of all data channels");
        
        try
        {
            foreach (var channel in DataChannelCentral.Channels.TakeWhile(channel => !cancellationToken.IsCancellationRequested))
            {
                try
                {
                    await channel.Value.Pipe.InitAsync();
                    _logger?.LogInformation("Successfully initialized channel: {ChannelId}", channel.Key);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to initialize channel: {ChannelId}", channel.Key);
                }
            }

            _logger?.LogInformation("Completed initialization of all data channels");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "An error occurred during channel initialization");
        }
    }

    /// <summary>
    /// Stops the service.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("DataChannelInitializerService is stopping");
        return Task.CompletedTask;
    }
} 