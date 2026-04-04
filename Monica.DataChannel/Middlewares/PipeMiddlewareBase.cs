using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Monica.DataChannel.Abstractions.Pipeline;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Middlewares;

/// <summary>
/// Base class for transform middleware.
/// </summary>
public abstract class PipelineTransformMiddlewareBase : IPipelineTransformMiddleware
{
    public virtual ChannelDataContext Pass(ChannelDataContext context) => context;
    public virtual Task<ChannelDataContext> PassAsync(ChannelDataContext context) => Task.FromResult(Pass(context));
    public dynamic GetMetadata()
    {
        return new
        {
            GetType().Name,
            GetType().FullName,
            GetType().AssemblyQualifiedName
        };
    }
}

/// <summary>
/// Base class for monitor middleware.
/// </summary>
public abstract class PipelineMonitorMiddlewareBase : IPipelineMonitorMiddleware
{
    public virtual ChannelDataContext Pass(ChannelDataContext context) => context;
    public virtual Task<ChannelDataContext> PassAsync(ChannelDataContext context) => Task.FromResult(Pass(context));
    public dynamic GetMetadata()
    {
        return new
        {
            GetType().Name,
            GetType().FullName,
            GetType().AssemblyQualifiedName
        };
    }

    public ChannelPipeline Pipe { get; set; } = null!;
    public virtual void CollectException(Exception exception, object? source = null, string? description = null,
        ILogger? logger = null)
    {
        Pipe.CollectException(exception, source ?? this, description);
        logger?.LogError(exception, description);
    }
}

/// <summary>
/// Base class for information-display middleware.
/// Provides a concurrent dictionary for storing and exposing statistics for the management UI.
/// Derived classes can write message-related information into the dictionary as needed.
/// </summary>
public abstract class PipelineInfoDisplayMiddlewareBase : PipelineMonitorMiddlewareBase
{
    /// <summary>
    /// Stores the information exposed by the middleware.
    /// Key: information identifier.
    /// Value: information payload of any supported type.
    /// </summary>
    protected readonly ConcurrentDictionary<string, object> InfoDictionary = new();

    /// <summary>
    /// Gets a read-only snapshot of the information dictionary.
    /// </summary>
    /// <returns>A read-only view of the information dictionary.</returns>
    public IReadOnlyDictionary<string, object> GetInfoDictionary()
    {
        return InfoDictionary.AsReadOnly();
    }

    /// <summary>
    /// Clears the information dictionary.
    /// </summary>
    public void ClearInfo()
    {
        InfoDictionary.Clear();
    }

    /// <summary>
    /// Sets an information entry.
    /// </summary>
    /// <param name="key">The information key.</param>
    /// <param name="value">The information value.</param>
    protected void SetInfo(string key, object value)
    {
        InfoDictionary.AddOrUpdate(key, value, (_, _) => value);
    }

    /// <summary>
    /// Gets an information entry.
    /// </summary>
    /// <param name="key">The information key.</param>
    /// <param name="defaultValue">The default value to return when the key is missing.</param>
    /// <returns>The information value.</returns>
    protected T GetInfo<T>(string key, T defaultValue = default!)
    {
        return InfoDictionary.TryGetValue(key, out var value) && value is T typedValue ? typedValue : defaultValue;
    }

    /// <summary>
    /// Increments a counter entry.
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="increment">The increment value. Defaults to <c>1</c>.</param>
    /// <returns>The updated counter value.</returns>
    protected long IncrementCounter(string key, long increment = 1)
    {
        return InfoDictionary.AddOrUpdate(key, increment, (_, existingValue) =>
        {
            if (existingValue is long longValue)
                return longValue + increment;
            if (existingValue is int intValue)
                return intValue + increment;
            return increment;
        }) as long? ?? increment;
    }

    /// <summary>
    /// Resets a counter entry.
    /// </summary>
    /// <param name="key">The counter key.</param>
    protected void ResetCounter(string key)
    {
        InfoDictionary.AddOrUpdate(key, 0L, (_, _) => 0L);
    }

    /// <summary>
    /// Overrides the metadata payload to include the information-display marker.
    /// </summary>
    /// <returns>Metadata that includes the information-display flag.</returns>
    public new dynamic GetMetadata()
    {
        var baseMetadata = base.GetMetadata();
        return new
        {
            baseMetadata.Name,
            baseMetadata.FullName,
            baseMetadata.AssemblyQualifiedName,
            IsInfoDisplayMiddleware = true
        };
    }
}
