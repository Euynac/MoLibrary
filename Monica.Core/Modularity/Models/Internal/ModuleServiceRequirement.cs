namespace Monica.Core.Modularity.Models.Internal;

/// <summary>
/// Identifies one finalized service dependency declared by a consuming module.
/// </summary>
internal sealed record ModuleServiceRequirement(
    Type ServiceType,
    bool IsKeyed,
    object? ServiceKey);
