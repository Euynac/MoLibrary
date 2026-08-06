namespace Monica.Core.Modularity.Annotations;

/// <summary>
/// Excludes a type from Monica's structural business-type discovery scan.
/// </summary>
/// <remarks>
/// Use this attribute for runtime-only infrastructure helper types that must remain available for
/// direct construction or factory use, but should not be exposed to module <c>TypeQuery</c> declarations.
/// Exclusion is global for the current host and is evaluated before any module query.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Delegate,
    AllowMultiple = false,
    Inherited = false)]
public sealed class ExcludeFromBusinessTypeDiscoveryAttribute : Attribute
{
}
