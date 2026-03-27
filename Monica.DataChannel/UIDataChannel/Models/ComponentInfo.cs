using Monica.DataChannel.BuildInMiddlewares;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.UIDataChannel.Models;

/// <summary>
/// Captures metadata about a pipeline component for UI display.
/// </summary>
public class ComponentInfo(IPipeComponent component)
{
    /// <summary>
    /// Component category.
    /// </summary>
    public EPipeComponentType Type { get; } = GetPipeComponentType(component.GetType());
    
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
        component is PipeInfoDisplayMiddlewareBase infoMiddleware ? infoMiddleware.GetInfoDictionary() : null;

    /// <summary>
    /// Determines the pipeline component category represented by the supplied type.
    /// </summary>
    /// <param name="type">Type of the component.</param>
    /// <returns>The matching <see cref="EPipeComponentType"/> value.</returns>
    public static EPipeComponentType GetPipeComponentType(Type type)
    {
        if (type.IsAssignableTo(typeof(IPipeEndpoint)))
        {
            return EPipeComponentType.Endpoint;
        }
        
        // Prefer info-display middleware because it inherits from the monitor middleware.
        if (type.IsAssignableTo(typeof(PipeInfoDisplayMiddlewareBase)))
        {
            return EPipeComponentType.InfoDisplayMiddleware;
        }
        
        if (type.IsAssignableTo(typeof(IPipeMonitorMiddleware)))
        {
            return EPipeComponentType.MonitorMiddleware;
        }
        
        if (type.IsAssignableTo(typeof(IPipeTransformMiddleware)))
        {
            return EPipeComponentType.TransformMiddleware;
        }
        
        if (type.IsAssignableTo(typeof(IPipeEndpointMiddleware)))
        {
            return EPipeComponentType.EndpointMiddleware;
        }
        
        // Fallback to the base middleware category when no specialized type matches.
        return EPipeComponentType.BaseMiddleware;
    }
}
