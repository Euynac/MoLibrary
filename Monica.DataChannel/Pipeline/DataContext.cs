using System.Dynamic;
using Monica.Tool.Extensions;

namespace Monica.DataChannel.Pipeline;

/// <summary>
/// Represents the data context that flows through a pipeline.
/// Wraps the payload together with its metadata during transport and processing.
/// Serves as the primary transfer unit inside the data pipeline.
/// </summary>
public class DataContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="DataContext"/>.
    /// </summary>
    /// <param name="source">The origin side of the data.</param>
    /// <param name="data">The payload.</param>
    public DataContext(EDataSource source, object? data)
    {
        Source = source;
        Data = data;
    }
    
    /// <summary>
    /// Gets or sets the side from which the data entered the pipeline.
    /// The value is either <see cref="EDataSource.Inner"/> or <see cref="EDataSource.Outer"/>.
    /// </summary>
    public EDataSource Source { get; set; }

    /// <summary>
    /// Gets or sets the metadata bag.
    /// Stores additional contextual information that endpoints or middleware can inspect and use.
    /// </summary>
    public ExpandoObject Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the payload.
    /// Represents the actual data being transported.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Gets the CLR type of the current payload.
    /// </summary>
    public Type? DataType => Data?.GetType();
    // TODO: Handle error payloads.

    /// <summary>
    /// Copies metadata from another data context into the current instance.
    /// </summary>
    /// <param name="data">The source data context.</param>
    /// <returns>The current data context instance.</returns>
    public DataContext CopyMetadata(DataContext data)
    {
        Metadata.Copy(data.Metadata);
        return this;
    }
}

/// <summary>
/// Identifies the side where the data originated and therefore the direction it travels in the channel.
/// Data originating from the inner endpoint is delivered to the outer endpoint, and vice versa.
/// </summary>
public enum EDataSource
{
    /// <summary>
    /// The data originated from the inner endpoint.
    /// </summary>
    Inner,

    /// <summary>
    /// The data originated from the outer endpoint.
    /// </summary>
    Outer
}
