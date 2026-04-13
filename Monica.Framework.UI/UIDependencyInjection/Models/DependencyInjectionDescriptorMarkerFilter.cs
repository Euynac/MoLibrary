namespace Monica.Framework.UI.UIDependencyInjection.Models;

/// <summary>
/// Defines additional descriptor marker filters used by the dependency-injection diagnostics page.
/// </summary>
public enum DependencyInjectionDescriptorMarkerFilter
{
    /// <summary>
    /// Show descriptors without keyed, warning, error, or rewrite markers.
    /// </summary>
    Standard,

    /// <summary>
    /// Show keyed descriptors.
    /// </summary>
    Keyed,

    /// <summary>
    /// Show descriptors with warning-level auto-registration issues.
    /// </summary>
    Warnings,

    /// <summary>
    /// Show descriptors with error-level auto-registration issues.
    /// </summary>
    Errors,

    /// <summary>
    /// Show rewritten descriptors.
    /// </summary>
    Rewritten
}
