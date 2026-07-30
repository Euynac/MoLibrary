namespace Monica.Core.ObjectMapping.Models;

/// <summary>
/// Selects the expression compiler used by the host-owned Mapster configuration.
/// </summary>
public enum ObjectMappingCompilerStrategy
{
    /// <summary>
    /// Uses the standard LINQ expression compiler supplied by Mapster.
    /// </summary>
    Default,

    /// <summary>
    /// Uses FastExpressionCompiler and fails eager validation when an expression is unsupported instead of silently
    /// falling back to the standard compiler.
    /// </summary>
    FastExpressionCompiler,

    /// <summary>
    /// Emits debuggable mapping assemblies through ExpressionDebugger for manual troubleshooting.
    /// </summary>
    Debug
}
