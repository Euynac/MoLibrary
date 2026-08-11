namespace Monica.Framework.Seeder.Models;

/// <summary>
/// Identifies where an effective seeder policy value was configured.
/// </summary>
public enum SeederPolicySource
{
    /// <summary>The value was inherited from <see cref="Monica.Modules.ModuleSeederOption"/>.</summary>
    ModuleDefault,

    /// <summary>The value was explicitly declared on the seeder.</summary>
    SeederOverride
}
