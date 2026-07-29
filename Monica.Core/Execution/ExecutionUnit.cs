namespace Monica.Core.Execution;

/// <summary>
/// Represents successful completion of an execution that has no domain result value.
/// </summary>
public readonly record struct ExecutionUnit
{
    /// <summary>
    /// Gets the reusable no-result value.
    /// </summary>
    public static ExecutionUnit Value => default;
}
