namespace MoLibrary.Generators.AutoController.Tool.Models;

/// <summary>
/// Configuration model for the RPC metadata generator tool.
/// </summary>
public class ToolConfig
{
    /// <summary>
    /// The root directory to search for metadata files.
    /// Supports both absolute and relative paths.
    /// </summary>
    public string ContextDirectory { get; set; } = string.Empty;

    /// <summary>
    /// The directory where generated JSON files will be written.
    /// Supports both absolute and relative paths.
    /// </summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// The name of the metadata file to search for.
    /// Default: __RpcMetadata.g.cs
    /// </summary>
    public string MetadataFileName { get; set; } = "__RpcMetadata.g.cs";

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ContextDirectory) || ContextDirectory.Contains("PLEASE_CONFIGURE"))
        {
            errors.Add("ContextDirectory is not configured");
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory) || OutputDirectory.Contains("PLEASE_CONFIGURE"))
        {
            errors.Add("OutputDirectory is not configured");
        }

        if (string.IsNullOrWhiteSpace(MetadataFileName))
        {
            errors.Add("MetadataFileName cannot be empty");
        }

        return errors.Count == 0;
    }
}
