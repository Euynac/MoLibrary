namespace Monica.Tool.Signing;

/// <summary>
/// Controls how signature fields are serialized before hashing.
/// </summary>
public enum SignatureMode
{
    /// <summary>
    /// Serializes fields as <c>key=value&amp;key2=value2</c> with URL-encoded values.
    /// </summary>
    QueryString,
}
