using Microsoft.Extensions.Logging;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.CoreCommunication;


/// <summary>
/// Base class for communication cores.
/// </summary>
public abstract class CommunicationCore : ICommunicationCore
{
    public abstract Task InitAsync(CancellationToken cancellationToken = default);
    public abstract Task DisposeAsync(CancellationToken cancellationToken = default);

    public void CollectException(Exception exception, object? source = null, string? description = null,
        ILogger? logger = null)
    {
        Pipe.CollectException(exception, source ?? this, description);
        logger?.LogError(exception, description);
    }
    public abstract EConnectionDirection SupportedConnectionDirection();

    /// <summary>
    /// Creates a data context originating from this endpoint.
    /// </summary>
    /// <param name="data">The payload.</param>
    /// <returns>The created data context.</returns>
    public virtual DataContext CreateData(object? data)
    {
        return new DataContext(EntranceType, data);
    }
    
    /// <summary>
    /// Sends the specified data context asynchronously.
    /// </summary>
    /// <param name="data">The data context to send.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    public async Task SendDataAsync(DataContext data)
    {
        DecorateDataContextBeforeSend(data);
        await Pipe.SendDataAsync(data);
    }

    /// <summary>
    /// Sends a raw payload asynchronously.
    /// </summary>
    /// <param name="data">The payload to send.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    public async Task SendDataAsync(object data)
    {
        var dataContext = CreateData(data);
        await SendDataAsync(dataContext);
    }

    /// <summary>
    /// Decorates the data context before it is sent.
    /// </summary>
    /// <param name="dataContext">The data context being sent.</param>
    protected virtual void DecorateDataContextBeforeSend(DataContext dataContext)
    {

    }
    public void SendData(DataContext data)
    {
        SendDataAsync(data).Wait();
    }
    public void SendData(object data)
    {
        SendDataAsync(data).Wait();
    }

    /// <summary>
    /// Gets or sets the owning pipeline instance.
    /// This property is guaranteed to be non-null after initialization.
    /// </summary>
    public DataPipeline Pipe { get; set; } = null!;

    /// <summary>
    /// Receives data asynchronously.
    /// </summary>
    /// <param name="data">The incoming data context.</param>
    /// <returns>A task that represents the asynchronous receive operation.</returns>
    public virtual async Task ReceiveDataAsync(DataContext data)
    {
        ReceiveData(data);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Receives data synchronously.
    /// </summary>
    /// <param name="data">The incoming data context.</param>
    public virtual void ReceiveData(DataContext data)
    {
    }

    public EDataSource EntranceType { get; set; }
    public abstract dynamic GetMetadata();
}

/// <summary>
/// <inheritdoc cref="CommunicationCore"/>
/// </summary>
/// <typeparam name="TMetadata">The metadata type used by the communication core.</typeparam>
/// <param name="metadata">The metadata instance.</param>
public abstract class CommunicationCore<TMetadata>(TMetadata metadata) : CommunicationCore where TMetadata : CommunicationMetadata
{
    public TMetadata Metadata { get; private set; } = metadata;

    public override Task InitAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public override Task DisposeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public override dynamic GetMetadata() => Metadata;
}
