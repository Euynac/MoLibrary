namespace Monica.Framework.Seeder.Models;

/// <summary>
/// Controls whether a seeder can overlap other ready seeders.
/// </summary>
public enum SeederExecutionMode
{
    /// <summary>
    /// Uses the host-level default configured by <see cref="Monica.Modules.ModuleSeederOption"/>.
    /// </summary>
    Inherit,

    /// <summary>
    /// Allows the seeder to run beside other concurrent seeders, subject to the host concurrency limit.
    /// </summary>
    Concurrent,

    /// <summary>
    /// Runs the seeder only after every previously scheduled seeder has completed and prevents any other seeder
    /// from starting until it completes.
    /// </summary>
    Exclusive
}
