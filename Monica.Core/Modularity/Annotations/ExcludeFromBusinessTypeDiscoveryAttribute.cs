namespace Monica.Core.Modularity.Annotations;

/// <summary>
/// Excludes a type from Monica module business-type iteration.
/// </summary>
/// <remarks>
/// Use this attribute for runtime-only infrastructure helper types that must remain available for
/// direct construction or factory use, but should not be treated as application business types by
/// modules implementing <c>IBusinessTypeIterator</c>.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Delegate,
    AllowMultiple = false,
    Inherited = false)]
public sealed class ExcludeFromBusinessTypeDiscoveryAttribute : Attribute
{
}
