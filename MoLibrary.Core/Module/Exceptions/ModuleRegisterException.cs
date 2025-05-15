using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Exceptions;

/// <summary>
/// Exception thrown when there are issues with module registration.
/// </summary>
public class ModuleRegisterException(string message) : ModuleException(message)
{
    /// <summary>
    /// List of missing configuration method keys.
    /// </summary>
    public List<string> MissingConfigKeys { get; set; } = [];

    /// <summary>
    /// The module that was being guided from when the error occurred.
    /// </summary>
    public EMoModules? GuideFrom { get; set; }
}