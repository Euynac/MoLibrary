namespace Monica.Tool.Extensions;

/// <summary>
/// Provides byte-array convenience extensions.
/// </summary>
public static class ByteArrayExtensions
{
    /// <summary>
    /// Wraps the byte array in a readable <see cref="MemoryStream"/>.
    /// </summary>
    public static MemoryStream ToMemoryStream(this byte[] bytes) => new(bytes);
}
