namespace Monica.Authority.Utils.HierarchyCode;

/// <summary>
/// Interface for entities that support hierarchical coding
/// </summary>
public interface IHasHierarchyCode
{
    /// <summary>
    /// Hierarchical Code of this entity.
    /// Example: "00001.00042.00005".
    /// This is a unique code for an entity in a hierarchy.
    /// It's changeable if hierarchy is changed.
    /// </summary>
    string Code { get; set; }
}