namespace MoLibrary.Generators.AutoController.Tool.Models;

/// <summary>
/// Information about a metadata file including path, assembly name, content and timestamps.
/// </summary>
public class MetadataFileInfo
{
    /// <summary>
    /// The full path to the metadata file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The assembly name extracted from the metadata.
    /// </summary>
    public required string AssemblyName { get; init; }

    /// <summary>
    /// The JSON content of the metadata.
    /// </summary>
    public required string JsonContent { get; init; }

    /// <summary>
    /// The last write time of the file.
    /// </summary>
    public DateTime LastWriteTime { get; init; }

    /// <summary>
    /// The creation time of the file.
    /// </summary>
    public DateTime CreationTime { get; init; }
}
