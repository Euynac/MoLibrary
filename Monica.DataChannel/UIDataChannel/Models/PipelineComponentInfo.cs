using Monica.DataChannel.Abstractions.Pipeline;
using Monica.DataChannel.Middlewares;

namespace Monica.DataChannel.UIDataChannel.Models;

/// <summary>
/// Captures metadata about a pipeline component for UI display.
/// </summary>
public class PipelineComponentInfo(IPipelineComponent component)
{
    /// <summary>
    /// Component category.
    /// </summary>
    public PipelineComponentKind Type { get; } = GetPipeComponentType(component.GetType());
    
    /// <summary>
    /// Display name of the component's CLR type.
    /// </summary>
    public string Name => component.GetType().Name;
    
    /// <summary>
    /// Metadata produced by the component.
    /// </summary>
    public object Metadata => component.GetMetadata();

    /// <summary>
    /// Dictionary of informational entries (only available for info-display middlewares).
    /// </summary>
    public IReadOnlyDictionary<string, object>? InfoDictionary => 
        component is PipelineInfoDisplayMiddlewareBase infoMiddleware ? infoMiddleware.GetInfoDictionary() : null;

    /// <summary>
    /// Determines the pipeline component category represented by the supplied type.
    /// </summary>
    /// <param name="type">Type of the component.</param>
    /// <returns>The matching <see cref="PipelineComponentKind"/> value.</returns>
    public static PipelineComponentKind GetPipeComponentType(Type type)
    {
        if (type.IsAssignableTo(typeof(IPipelineEndpoint)))
        {
            return PipelineComponentKind.Endpoint;
        }
        
        // Prefer info-display middleware because it inherits from the monitor middleware.
        if (type.IsAssignableTo(typeof(PipelineInfoDisplayMiddlewareBase)))
        {
            return PipelineComponentKind.InfoDisplayMiddleware;
        }
        
        if (type.IsAssignableTo(typeof(IPipelineMonitorMiddleware)))
        {
            return PipelineComponentKind.MonitorMiddleware;
        }
        
        if (type.IsAssignableTo(typeof(IPipelineTransformMiddleware)))
        {
            return PipelineComponentKind.TransformMiddleware;
        }
        
        if (type.IsAssignableTo(typeof(IPipelineEndpointMiddleware)))
        {
            return PipelineComponentKind.EndpointMiddleware;
        }
        
        // Fallback to the base middleware category when no specialized type matches.
        return PipelineComponentKind.BaseMiddleware;
    }
}
