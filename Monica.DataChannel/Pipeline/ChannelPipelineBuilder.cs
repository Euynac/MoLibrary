using Microsoft.Extensions.DependencyInjection;

using Monica.Core.ObservableInstance.Abstractions;
using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Abstractions.Pipeline;
using Monica.DataChannel.Providers.Default;

namespace Monica.DataChannel.Pipeline;

/// <summary>
/// Builder for data pipelines.
/// Provides a fluent API for creating and configuring data pipelines.
/// Assembles endpoints, middleware, and other components into a complete pipeline.
/// </summary>
public class ChannelPipelineBuilder
{
    private CommunicationOptions? _innerEndpointMetadata;
    private CommunicationOptions? _outerEndpointMetadata;

    /// <summary>
    /// Gets the communication core type used for the inner endpoint.
    /// </summary>
    public Type? InnerCoreType { get; private set; }

    /// <summary>
    /// Gets the communication core type used for the outer endpoint.
    /// </summary>
    public Type? OuterCoreType { get; private set; }

    private readonly List<IPipelineMiddleware> _middlewares = [];
    
    /// <summary>
    /// Gets the middleware instances that were added directly to the builder.
    /// </summary>
    public IReadOnlyList<IPipelineMiddleware> Middlewares => _middlewares;

    /// <summary>
    /// Stores middleware types that should be resolved from dependency injection at build time.
    /// </summary>
    private readonly List<Type> _diMiddlewares = [];

    /// <summary>
    /// Gets or sets the pipeline registration identifier.
    /// Used to uniquely identify the pipeline.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the pipeline group identifier.
    /// Used to organize related pipelines together.
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// Sets the outer communication endpoint to the default endpoint implementation.
    /// </summary>
    /// <typeparam name="TCore">The outer communication endpoint type, which must inherit from <see cref="DefaultChannelEndpoint"/>.</typeparam>
    /// <returns>The current builder instance.</returns>
    public ChannelPipelineBuilder SetOuterEndpoint<TCore>() where TCore : DefaultChannelEndpoint
    {
        OuterCoreType = typeof(TCore);
        return this;
    }

    /// <summary>
    /// Sets the inner communication endpoint to the default endpoint implementation.
    /// </summary>
    /// <typeparam name="TCore">The inner communication endpoint type, which must inherit from <see cref="DefaultChannelEndpoint"/>.</typeparam>
    /// <returns>The current builder instance.</returns>
    public ChannelPipelineBuilder SetInnerEndpoint<TCore>() where TCore : DefaultChannelEndpoint
    {
        InnerCoreType = typeof(TCore);
        return this;
    }

    /// <summary>
    /// Sets the inner communication endpoint.
    /// When not configured, the builder falls back to the default inner endpoint implementation, which ignores incoming outer messages.
    /// </summary>
    /// <param name="metadata">The communication metadata that describes the endpoint configuration.</param>
    /// <returns>The current builder instance.</returns>
    public ChannelPipelineBuilder SetInnerEndpoint(CommunicationOptions metadata)
    {
        metadata.EnrichOrValidate();
        _innerEndpointMetadata = metadata;
        InnerCoreType = _innerEndpointMetadata.GetCommunicationCoreType();
        return this;
    }

    /// <summary>
    /// Sets the outer communication endpoint.
    /// This endpoint is required before the pipeline can be built.
    /// </summary>
    /// <param name="metadata">The communication metadata that describes the endpoint configuration.</param>
    /// <returns>The current builder instance.</returns>
    public ChannelPipelineBuilder SetOuterEndpoint(CommunicationOptions metadata)
    {
        metadata.EnrichOrValidate();
        _outerEndpointMetadata = metadata;
        OuterCoreType = _outerEndpointMetadata.GetCommunicationCoreType();
        return this;
    }

    /// <summary>
    /// Adds middleware that should be resolved from dependency injection.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type, which must implement <see cref="IPipelineMiddleware"/>.</typeparam>
    /// <returns>The current builder instance.</returns>
    public ChannelPipelineBuilder AddPipeMiddleware<TMiddleware>() where TMiddleware : class, IPipelineMiddleware
    {
        _diMiddlewares.Add(typeof(TMiddleware));
        return this;
    }

    /// <summary>
    /// Adds middleware instances directly to the pipeline.
    /// </summary>
    /// <param name="middlewares">The middleware instances to add.</param>
    /// <returns>The current builder instance.</returns>
    public ChannelPipelineBuilder AddPipeMiddleware(params IPipelineMiddleware[] middlewares)
    {
        _middlewares.AddRange(middlewares);
        return this;
    }

    /// <summary>
    /// Registers the configured pipeline builder with <see cref="DataChannelCentral"/>.
    /// </summary>
    /// <param name="id">The unique pipeline identifier.</param>
    /// <param name="groupId">An optional pipeline group identifier.</param>
    /// <exception cref="Exception">Thrown when the outer endpoint has not been configured.</exception>
    public void Register(string id, string? groupId = null)
    {
        if (OuterCoreType == null) throw new Exception("You must set outer endpoint for data pipeline");
        if (_innerEndpointMetadata == null && InnerCoreType == null)
        {
            SetInnerEndpoint(new DefaultEndpointOptions());
        }
        Id = id;
        GroupId = groupId;
        DataChannelCentral.RegisterBuilder(this);
    }

    /// <summary>
    /// Builds the data pipeline.
    /// Creates and connects all endpoints and middleware to produce a complete data pipeline.
    /// Components marked as transient are wrapped in proxies.
    /// </summary>
    /// <param name="provider">The service provider used to resolve dependencies.</param>
    /// <returns>The fully built data pipeline instance.</returns>
    internal ChannelPipeline Build(IServiceProvider provider)
    {
        // Create the inner endpoint.
        var innerEndpoint = TransientProxy.CreateEndpointProxy(provider, InnerCoreType!, ChannelSide.Inner, _innerEndpointMetadata);

        // Create the outer endpoint.
        var outerEndpoint = TransientProxy.CreateEndpointProxy(provider, OuterCoreType!, ChannelSide.Outer, _outerEndpointMetadata);
        outerEndpoint.EntranceType = ChannelSide.Outer;

        // Resolve the observable instance registry.
        var observableManager = provider.GetRequiredService<IObservableInstanceRegistry>();

        // Create the pipeline.
        var pipe = new ChannelPipeline(innerEndpoint, outerEndpoint, Id, observableManager, GroupId);

        // Start with directly registered middleware.
        var middlewaresList = new List<IPipelineMiddleware>(_middlewares);

        // Add middleware resolved from dependency injection.
        foreach (var type in _diMiddlewares)
        {
            var middleware = TransientProxy.CreateMiddlewareProxy(provider, type);
            middlewaresList.Add(middleware);
        }

        pipe.SetMiddlewares(middlewaresList);
        DataChannelCentral.RegisterPipeline(pipe);
        return pipe;
    }
}
