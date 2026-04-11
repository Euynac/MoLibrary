namespace Monica.DependencyInjection.Models;

/// <summary>
/// Represents the severity Monica assigned to one dependency-injection diagnostic.
/// </summary>
public enum DependencyInjectionDiagnosticSeverity
{
    /// <summary>
    /// Indicates a non-fatal condition that still produced a registration result.
    /// </summary>
    Warning,

    /// <summary>
    /// Indicates a registration problem that prevented Monica from producing the expected descriptor.
    /// </summary>
    Error
}
