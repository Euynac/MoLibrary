namespace Monica.Framework.UI.UIDependencyInjection.Models;

/// <summary>
/// Defines the high-level descriptor scope filter used by the dependency-injection diagnostics page.
/// </summary>
public enum DependencyInjectionDescriptorScopeFilter
{
    /// <summary>
    /// Show only descriptors created by Monica conventional registration.
    /// </summary>
    AutoRegistered,

    /// <summary>
    /// Show descriptors that were not created by Monica conventional registration.
    /// </summary>
    Other
}
