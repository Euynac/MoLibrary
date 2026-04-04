using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Abstractions.Pipeline;

/// <summary>
/// Base contract for pipeline middleware.
/// Middleware components are singletons unless they declare a transient lifetime through <see cref="ITransientChannelComponent"/>.
/// Middleware can intercept, modify, or transform the data flow.
/// </summary>
public interface IPipelineMiddleware : IPipelineComponent
{
}

/// <summary>
/// Contract for transform middleware in the pipeline.
/// Responsible for transforming, processing, or filtering data as it flows through the pipeline.
/// Each <see cref="ChannelDataContext"/> passes through transform middleware in sequence.
/// </summary>
public interface IPipelineTransformMiddleware : IPipelineMiddleware
{
    /// <summary>
    /// Processes a data context as it passes through the middleware.
    /// </summary>
    /// <param name="context">The data context to process.</param>
    /// <returns>The processed data context.</returns>
    public Task<ChannelDataContext> PassAsync(ChannelDataContext context);
}

/// <summary>
/// Contract for endpoint middleware in the pipeline.
/// Designed specifically for endpoint-related behavior and configuration.
/// Can enhance or alter endpoint behavior.
/// </summary>
public interface IPipelineEndpointMiddleware : IPipelineMiddleware, IPipelineAware
{
}

/// <summary>
/// Contract for monitoring middleware in the pipeline.
/// Provides end-to-end monitoring and control capabilities by combining transform behavior with pipeline access.
/// </summary>
public interface IPipelineMonitorMiddleware : IPipelineTransformMiddleware, IPipelineAware
{
}
