namespace Monica.Core.Modularity.Models;

/// <summary>
/// Defines how module registration handles duplicate requests with the same execution identity.
/// </summary>
public enum ModuleConfigurationDuplicateBehavior
{
    /// <summary>
    /// Skips the duplicate request and logs a warning because the duplicate was not declared as intentional.
    /// </summary>
    Warn,

    /// <summary>
    /// Skips the duplicate request without logging because repeating the request is harmless and expected.
    /// </summary>
    SilentIdempotent,

    /// <summary>
    /// Treats duplicate requests as explicit alternatives where the last request in registration order wins.
    /// </summary>
    ExclusiveLastWins
}
