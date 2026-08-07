namespace Monica.Core.Modularity.Models;

/// <summary>
/// Selects a named ordering band for an explicit lifecycle contribution within one module.
/// </summary>
public enum ModuleRegistrationOrder
{
    /// <summary>
    /// Runs before the module strategy's own lifecycle callback.
    /// </summary>
    BeforeModule = -100,

    /// <summary>
    /// Runs after the module strategy's own lifecycle callback. This is the normal extension point.
    /// </summary>
    AfterModule = 0,

    /// <summary>
    /// Runs after normal explicit contributions.
    /// </summary>
    Late = 100
}
