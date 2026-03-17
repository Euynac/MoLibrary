using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Dashboard.Models;

/// <summary>
/// Represents the module dependency graph.
/// </summary>
public class ModuleDependencyGraph
{
    /// <summary>
    /// All nodes in the graph.
    /// </summary>
    public List<ModuleDependencyNode> Nodes { get; set; } = [];

    /// <summary>
    /// All dependency edges in the graph.
    /// </summary>
    public List<ModuleDependencyEdge> Edges { get; set; } = [];

    /// <summary>
    /// Indicates whether the graph contains circular dependencies.
    /// </summary>
    public bool HasCircularDependencies { get; set; }

    /// <summary>
    /// Circular dependency paths, if any.
    /// </summary>
    public List<List<ModuleKey>> CircularDependencyPaths { get; set; } = [];

    /// <summary>
    /// Topological order of the modules when no cycle exists.
    /// </summary>
    public List<ModuleKey> TopologicalOrder { get; set; } = [];

    /// <summary>
    /// Module layers grouped by dependency depth.
    /// </summary>
    public Dictionary<int, List<ModuleKey>> ModuleLayers { get; set; } = [];
}

/// <summary>
/// Node in the module dependency graph.
/// </summary>
public class ModuleDependencyNode
{
    /// <summary>
    /// Module key.
    /// </summary>
    public ModuleKey Module { get; set; }

    /// <summary>
    /// Module display name.
    /// </summary>
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>
    /// Module type name.
    /// </summary>
    public string ModuleTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the module is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Number of directly required modules.
    /// </summary>
    public int DirectDependencyCount { get; set; }

    /// <summary>
    /// Number of total dependencies, including transitive dependencies.
    /// </summary>
    public int TotalDependencyCount { get; set; }

    /// <summary>
    /// Number of modules that depend on this module.
    /// </summary>
    public int DependentModuleCount { get; set; }

    /// <summary>
    /// Layer in the dependency graph. `0` means no dependencies; larger values indicate deeper dependency chains.
    /// </summary>
    public int Layer { get; set; }

    /// <summary>
    /// Indicates whether the node participates in a cycle.
    /// </summary>
    public bool IsPartOfCycle { get; set; }

    /// <summary>
    /// Current module phase.
    /// </summary>
    public EMoModuleConfigMethods Status { get; set; }
}

/// <summary>
/// Edge in the module dependency graph.
/// </summary>
public class ModuleDependencyEdge
{
    /// <summary>
    /// Source module that depends on the target.
    /// </summary>
    public ModuleKey SourceModule { get; set; }

    /// <summary>
    /// Target module being depended on.
    /// </summary>
    public ModuleKey TargetModule { get; set; }

    /// <summary>
    /// Dependency kind.
    /// </summary>
    public DependencyType DependencyType { get; set; }

    /// <summary>
    /// Indicates whether the edge participates in a cycle.
    /// </summary>
    public bool IsPartOfCycle { get; set; }
}

/// <summary>
/// Kind of dependency represented by an edge.
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// Direct dependency.
    /// </summary>
    Direct,

    /// <summary>
    /// Indirect or transitive dependency.
    /// </summary>
    Transitive,

    /// <summary>
    /// Circular dependency.
    /// </summary>
    Circular
} 
