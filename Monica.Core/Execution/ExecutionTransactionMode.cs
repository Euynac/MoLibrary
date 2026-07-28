namespace Monica.Core.Execution;

/// <summary>
/// Defines whether an execution boundary participates in Monica's automatic transaction behavior.
/// </summary>
public enum ExecutionTransactionMode
{
    /// <summary>
    /// Does not create an automatic outer unit of work. The operation may still create explicit scopes when needed.
    /// </summary>
    None,

    /// <summary>
    /// Allows the registered unit-of-work behavior to wrap the complete execution boundary.
    /// </summary>
    Automatic
}
