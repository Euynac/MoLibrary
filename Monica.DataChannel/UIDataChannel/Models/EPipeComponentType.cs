namespace Monica.DataChannel.UIDataChannel.Models;

/// <summary>
/// Enum describing pipeline component roles for the UI.
/// </summary>
public enum EPipeComponentType
{
    /// <summary>
    /// Represents a pipeline endpoint component.
    /// </summary>
    Endpoint,
    
    /// <summary>
    /// Represents a transformation middleware.
    /// </summary>
    TransformMiddleware,
    
    /// <summary>
    /// Represents middleware that wraps an endpoint.
    /// </summary>
    EndpointMiddleware,
    
    /// <summary>
    /// Represents monitoring middleware.
    /// </summary>
    MonitorMiddleware,
    
    /// <summary>
    /// Represents the base middleware implementation.
    /// </summary>
    BaseMiddleware,
    
    /// <summary>
    /// Represents middleware that provides info dashboards.
    /// </summary>
    InfoDisplayMiddleware
}
