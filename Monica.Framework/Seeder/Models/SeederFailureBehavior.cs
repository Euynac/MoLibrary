namespace Monica.Framework.Seeder.Models;

/// <summary>
/// Defines how startup seeding responds to an individual seeder failure.
/// </summary>
public enum SeederFailureBehavior
{
    /// <summary>
    /// Records the failure, continues with remaining seeders, and allows host startup to complete.
    /// </summary>
    ContinueStartup,

    /// <summary>
    /// Propagates the first seeder failure and prevents host startup from completing.
    /// </summary>
    FailStartup
}
