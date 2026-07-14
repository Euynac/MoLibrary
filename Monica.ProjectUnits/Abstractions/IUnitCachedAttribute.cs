namespace Monica.ProjectUnits.Abstractions;

/// <summary>
/// Marks an attribute whose instance should be retained in discovered project-unit metadata.
/// </summary>
/// <remarks>
/// Implementations must be attributes applied to business types. ProjectUnits preserves the instances for diagnostics,
/// UI projection, and catalog integrations; implementations should therefore contain immutable metadata only.
/// </remarks>
public interface IUnitCachedAttribute
{
}
