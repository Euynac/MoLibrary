using Microsoft.Extensions.Logging;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Interfaces;

/// <summary>
/// Allows a component to access the pipeline that owns it.
/// Implementing components, such as middleware and endpoints, receive the pipeline reference during initialization.
/// </summary>
public interface IWantAccessPipeline
{
    /// <summary>
    /// Gets or sets the owning pipeline instance.
    /// This reference can be used to access pipeline functionality and related components.
    /// </summary>
    public DataPipeline Pipe { get; set; }


    /// <summary>
    /// Collects exception details into the pipeline exception store.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="source">The source object of the exception. Defaults to the current instance.</param>
    /// <param name="description">An optional description of the exception.</param>
    /// <param name="logger">An optional logger used to record the exception.</param>
    public void CollectException(Exception exception, object? source = null, string? description = null,
        ILogger? logger = null);
}
