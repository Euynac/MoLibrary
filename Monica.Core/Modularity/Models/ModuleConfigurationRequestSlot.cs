namespace Monica.Core.Modularity.Models;

/// <summary>
/// Distinguishes requests that share a guide method key but configure different execution surfaces.
/// </summary>
public enum ModuleConfigurationRequestSlot
{
    /// <summary>
    /// A normal executable module request such as service, builder, middleware, or endpoint configuration.
    /// </summary>
    Execution,

    /// <summary>
    /// A module option configuration request registered into Microsoft.Extensions.Options.
    /// </summary>
    Option
}
