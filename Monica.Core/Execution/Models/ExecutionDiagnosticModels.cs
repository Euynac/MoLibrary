namespace Monica.Core.Execution.Models;

/// <summary>
/// Identifies one execution operation without exposing its canonical assembly-scoped key.
/// </summary>
/// <param name="Value">The compact deterministic identifier.</param>
public readonly record struct ExecutionOperationId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>
/// Identifies one execution plan without exposing its canonical assembly-scoped key.
/// </summary>
/// <param name="Value">The compact deterministic identifier.</param>
public readonly record struct ExecutionPlanId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>
/// Carries the exact CLR identity used only for advanced diagnostics.
/// </summary>
/// <param name="AssemblyQualifiedName">The assembly-qualified CLR type identity.</param>
public sealed record ExecutionTypeDiagnosticsSnapshot(string AssemblyQualifiedName);

/// <summary>
/// Provides compact and fully qualified display names for a CLR type while isolating its machine identity.
/// </summary>
/// <param name="Name">The compact clean type name.</param>
/// <param name="FullName">The clean namespace-qualified type name.</param>
/// <param name="AssemblyName">The simple assembly name without version metadata.</param>
/// <param name="Diagnostics">The exact identity reserved for advanced diagnostics.</param>
public sealed record ExecutionTypeSnapshot(
    string Name,
    string FullName,
    string AssemblyName,
    ExecutionTypeDiagnosticsSnapshot Diagnostics);

/// <summary>
/// Carries the exact method identity used only for advanced diagnostics.
/// </summary>
/// <param name="CanonicalSignature">The canonical signature including the assembly-scoped declaring type.</param>
public sealed record ExecutionMethodDiagnosticsSnapshot(string CanonicalSignature);

/// <summary>
/// Describes an execution entry method using readable CLR names.
/// </summary>
/// <param name="Name">The compact method name, including generic arguments when present.</param>
/// <param name="DisplaySignature">The clean developer-facing method signature.</param>
/// <param name="Diagnostics">The exact identity reserved for advanced diagnostics.</param>
public sealed record ExecutionMethodSnapshot(
    string Name,
    string DisplaySignature,
    ExecutionMethodDiagnosticsSnapshot Diagnostics);

/// <summary>
/// Carries the canonical assembly-scoped identity of an operation for advanced diagnostics.
/// </summary>
/// <param name="CanonicalKey">The exact operation key used for aggregation.</param>
public sealed record ExecutionOperationDiagnosticsSnapshot(string CanonicalKey);

/// <summary>
/// Carries the canonical assembly-scoped identity of a plan for advanced diagnostics.
/// </summary>
/// <param name="CanonicalKey">The exact plan key used for diagnostics.</param>
public sealed record ExecutionPlanDiagnosticsSnapshot(string CanonicalKey);
