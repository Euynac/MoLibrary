
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Core.ObservableInstance.Models;
using Monica.DataChannel.CoreCommunication;
using Monica.DataChannel.Interfaces;
using Monica.Tool.Extensions;

namespace Monica.DataChannel.Pipeline;

/// <summary>
/// Represents a data pipeline.
/// Acts as the core component of a data channel and is responsible for data transport, transformation, and processing.
/// Manages how endpoints and middleware are connected and coordinated.
/// </summary>
public class DataPipeline : IObservableInstance
{
    /// <summary>
    /// Gets or sets the inner endpoint.
    /// Handles data flowing from the outer side into the inner side.
    /// </summary>
    public IPipeEndpoint InnerEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the outer endpoint.
    /// Handles data flowing from the inner side out to the outer side.
    /// </summary>
    public IPipeEndpoint OuterEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the unique pipeline identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the pipeline group identifier.
    /// Used to organize related pipelines together.
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// Gets a value indicating whether initialization completed successfully.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Gets a value indicating whether initialization is currently in progress.
    /// </summary>
    public bool IsInitializing { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the pipeline is unavailable.
    /// This is set to <see langword="true"/> when initialization fails or a fatal error occurs.
    /// </summary>
    public bool IsNotAvailable { get; set; }

    /// <summary>
    /// Gets or sets the observable tracker used to record runtime state and exceptions.
    /// </summary>
    public ObservableInstanceTracker ObservableTracker { get; set; } = null!;

    /// <summary>
    /// Gets a value indicating whether the pipeline has recorded exceptions.
    /// </summary>
    public bool HasExceptions => ObservableTracker?.HasExceptions ?? false;

    /// <summary>
    /// Initializes a new instance of <see cref="DataPipeline"/>.
    /// </summary>
    /// <param name="innerEndpoint">The inner endpoint.</param>
    /// <param name="outerEndpoint">The outer endpoint.</param>
    /// <param name="id">The pipeline identifier.</param>
    /// <param name="observableManager">The observable instance registry.</param>
    /// <param name="groupId">An optional pipeline group identifier.</param>
    internal DataPipeline(
        IPipeEndpoint innerEndpoint,
        IPipeEndpoint outerEndpoint,
        string id,
        IObservableInstanceRegistry observableManager,
        string? groupId = null)
    {
        InnerEndpoint = innerEndpoint;
        OuterEndpoint = outerEndpoint;
        Id = id;
        GroupId = groupId;

        // Register the pipeline with the observable tracker.
        ObservableTracker = observableManager.Register(id, opt =>
        {
            opt.MaxHistorySize = DataChannelCentral.Setting.RecentExceptionToKeep;
            opt.InstanceName = id;
            opt.InstanceType = typeof(DataPipeline);
            opt.GroupId = groupId;
            opt.Logger = DataChannelCentral.Logger;
        });
    }

    /// <summary>
    /// Creates a new data pipeline builder.
    /// </summary>
    /// <returns>A new <see cref="DataPipelineBuilder"/> instance.</returns>
    public static DataPipelineBuilder Create() => new();

    /// <summary>
    /// Gets the endpoint middleware collection.
    /// These middleware components participate in endpoint-related behavior.
    /// </summary>
    public List<IPipeEndpointMiddleware> EndpointMiddlewares { get; private set; } = [];

    /// <summary>
    /// Gets the transform middleware collection.
    /// These middleware components handle data transformation and processing.
    /// </summary>
    public List<IPipeTransformMiddleware> TransformMiddlewares { get; private set; } = [];

    /// <summary>
    /// Enumerates all middleware registered in the pipeline.
    /// </summary>
    /// <returns>An enumeration of all pipeline middleware.</returns>
    internal IEnumerable<IPipeMiddleware> GetMiddlewares()
    {
        foreach (var endpointMiddleware in EndpointMiddlewares)
        {
            yield return endpointMiddleware;
        }

        foreach (var transformMiddleware in TransformMiddlewares)
        {
            yield return transformMiddleware;
        }
    }

    /// <summary>
    /// Enumerates all endpoints in the pipeline.
    /// </summary>
    /// <returns>An enumeration of all pipeline endpoints.</returns>
    internal IEnumerable<IPipeEndpoint> GetEndpoints()
    {
        yield return InnerEndpoint;
        yield return OuterEndpoint;
    }

    /// <summary>
    /// Enumerates all components in the pipeline, including endpoints and middleware.
    /// </summary>
    /// <returns>An enumeration of all pipeline components.</returns>
    internal IEnumerable<IPipeComponent> GetComponents()
    {
        foreach (var endpoint in GetEndpoints())
        {
            yield return endpoint;
        }

        foreach (var middleware in GetMiddlewares())
        {
            yield return middleware;
        }
    }

    /// <summary>
    /// Assigns middleware to the appropriate internal collections based on type.
    /// </summary>
    /// <param name="middlewares">The middleware collection to assign.</param>
    internal void SetMiddlewares(IReadOnlyList<IPipeMiddleware> middlewares)
    {
        EndpointMiddlewares = middlewares.OfType<IPipeEndpointMiddleware>().ToList();
        TransformMiddlewares = middlewares.OfType<IPipeTransformMiddleware>().ToList();
    }

    /// <summary>
    /// Records exception details in the observable tracker.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="source">The source object that caused the exception.</param>
    /// <param name="description">An optional description of the exception.</param>
    public void CollectException(Exception exception, object source, string? description = null)
    {
        var message = description ?? exception.Message;
        ObservableTracker.RecordState(message, PipelineState.Error, exception);
    }

    /// <summary>
    /// Records a pipeline state transition.
    /// </summary>
    /// <param name="state">The new state.</param>
    /// <param name="message">The state description.</param>
    public void RecordState(PipelineState state, string message)
    {
        ObservableTracker.RecordState(message, state);
    }

    /// <summary>
    /// Records an exception and switches the pipeline to the error state.
    /// </summary>
    /// <param name="exception">The exception instance.</param>
    /// <param name="message">The exception description.</param>
    public void RecordException(Exception exception, string message)
    {
        ObservableTracker.RecordState(message, PipelineState.Error, exception);
    }

    /// <summary>
    /// Gets all recorded exceptions.
    /// Kept for backward compatibility.
    /// </summary>
    public IReadOnlyList<ObservableStateEntry> GetExceptions()
    {
        return ObservableTracker.GetExceptions();
    }

    /// <summary>
    /// Gets the most recent exception records.
    /// Kept for backward compatibility.
    /// </summary>
    public IReadOnlyList<ObservableStateEntry> GetRecentExceptions(int count)
    {
        return ObservableTracker.GetRecentExceptions(count);
    }

    /// <summary>
    /// Sends data through the pipeline.
    /// The data is processed by transform middleware and then dispatched to the opposite endpoint based on its source side.
    /// </summary>
    /// <param name="data">The data context to send.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    public async Task SendDataAsync(DataContext data)
    {
        // Run transform middleware.
        await TransformMiddlewares.DoAsync(async p =>
        {
            try
            {
                data = await p.PassAsync(data);
            }
            catch (Exception ex)
            {
                CollectException(ex, p);
                throw; // Rethrow to preserve the existing behavior.
            }
        });


        // Dispatch to the target endpoint.
        try
        {
            if (data.Source == EDataSource.Outer)
            {
                await InnerEndpoint.ReceiveDataAsync(data);
            }
            else if(data.Source == EDataSource.Inner)
            {
                await OuterEndpoint.ReceiveDataAsync(data);
            }
        }
        catch (Exception ex)
        {
            var targetEndpoint = data.Source == EDataSource.Outer ? InnerEndpoint : OuterEndpoint;
            CollectException(ex, targetEndpoint);
            throw; // Rethrow to preserve the existing behavior.
        }
    }

    /// <summary>
    /// Initializes the pipeline and its components.
    /// Assigns pipeline references and initializes all communication cores.
    /// </summary>
    /// <returns>A task that represents the initialization operation.</returns>
    internal async Task InitAsync(CancellationToken cancellationToken = default)
    {
        if(IsInitializing)
        {
            return;
        }
        IsInitializing = true;
        InnerEndpoint.Pipe = this;
        OuterEndpoint.Pipe = this;
        GetMiddlewares().OfType<IWantAccessPipeline>().Do(p => p.Pipe = this);
        
        foreach (var communicationCore in GetEndpoints().OfType<ICommunicationCore>())
        {
            try
            {
                await communicationCore.InitAsync(cancellationToken);
            }
            catch (Exception e)
            {
                // Record the initialization failure.
                CollectException(e, communicationCore, $"DataPipeline:{Id}，初始化通信核心时发生错误，通信核心：{communicationCore.GetType().Name}");
                
                IsNotAvailable = true;
                IsInitializing = false;
                throw;
            }
        }

        IsInitialized = true;
        IsInitializing = false;
    }

    /// <summary>
    /// Releases pipeline resources and disposes its components.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    internal async Task DisposeAsync()
    {
        await GetEndpoints().OfType<ICommunicationCore>().DoAsync(async p => await p.DisposeAsync());
        ObservableTracker?.Dispose();
        IsInitialized = false;
    }
}
