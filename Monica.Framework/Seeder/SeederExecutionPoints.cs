using Monica.Core.Execution;

namespace Monica.Framework.Seeder;

/// <summary>
/// Stable execution points emitted by the background seeder scheduler.
/// </summary>
public static class SeederExecutionPoints
{
    /// <summary>
    /// Represents one finite seeder invocation.
    /// </summary>
    public static readonly ExecutionPoint Run = new("seeder.run");
}
