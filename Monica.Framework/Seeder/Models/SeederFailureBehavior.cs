namespace Monica.Framework.Seeder.Models;

/// <summary>
/// Controls how the scheduler responds after a seeder exhausts its configured attempts.
/// </summary>
public enum SeederFailureBehavior
{
    /// <summary>
    /// Uses the host-level default configured by <see cref="Monica.Modules.ModuleSeederOption"/>.
    /// </summary>
    Inherit,

    /// <summary>
    /// Records the failure, blocks dependent seeders, and continues scheduling independent work.
    /// </summary>
    ContinueAndRecord,

    /// <summary>
    /// Aborts the current seeder run while leaving the host alive so diagnostics remain available.
    /// </summary>
    FailFast
}
