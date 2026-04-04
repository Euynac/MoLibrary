using Monica.Repository.GuidGeneration.Services;

namespace Monica.Repository.GuidGeneration.Models;

/// <summary>
/// Configures how sequential GUID values are generated when <see cref="SequentialGuidGenerator" /> is used.
/// </summary>
public class SequentialGuidGeneratorOptions
{
    /// <summary>
    /// Optional default layout used when callers request a sequential GUID without specifying a layout explicitly.
    /// When left unset, <see cref="SequentialGuidLayout.SequentialAtEnd" /> is used.
    /// </summary>
    public SequentialGuidLayout? DefaultSequentialGuidLayout { get; set; }

    /// <summary>
    /// Resolves the effective default layout for sequential GUID generation.
    /// </summary>
    public SequentialGuidLayout GetDefaultSequentialGuidLayout()
    {
        return DefaultSequentialGuidLayout ??
               SequentialGuidLayout.SequentialAtEnd;
    }
}
