namespace MoLibrary.Dapr.EventBus.Models;

/// <summary>
/// This class defines the metadata for subscribe endpoint.
/// </summary>
internal class MoMetadata : Dictionary<string, string>
{
    /// <summary>
    /// Initializes a new instance of the Metadata class.
    /// </summary>
    public MoMetadata() { }

    /// <summary>
    /// Initializes a new instance of the Metadata class.
    /// </summary>
    /// <param name="dictionary"></param>
    public MoMetadata(IDictionary<string, string> dictionary) : base(dictionary) { }

    /// <summary>
    /// RawPayload key
    /// </summary>
    internal const string RawPayload = "rawPayload";
}