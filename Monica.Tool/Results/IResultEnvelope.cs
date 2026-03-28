using System.Dynamic;

namespace Monica.Tool.Results;

/// <summary>
/// Represents the shared envelope contract for Monica result models.
/// </summary>
public interface IResultEnvelope
{
    /// <summary>
    /// Gets or sets the response message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the result status.
    /// </summary>
    public ResStatus Status { get; set; }

    /// <summary>
    /// Gets or sets additional debug metadata such as chain-tracing details.
    /// </summary>
    public ExpandoObject? Metadata { get; set; }
}
