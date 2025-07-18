using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Dashboard.Models;

/// <summary>
/// 表示模块依赖关系图信息。
/// </summary>
public class ModuleDependencyGraph
{
    /// <summary>
    /// 图中的所有节点（模块）
    /// </summary>
    public List<ModuleDependencyNode> Nodes { get; set; } = [];

    /// <summary>
    /// 图中的所有边（依赖关系）
    /// </summary>
    public List<ModuleDependencyEdge> Edges { get; set; } = [];

    /// <summary>
    /// 是否存在循环依赖
    /// </summary>
    public bool HasCircularDependencies { get; set; }

    /// <summary>
    /// 循环依赖路径（如果存在）
    /// </summary>
    public List<List<EMoModules>> CircularDependencyPaths { get; set; } = [];

    /// <summary>
    /// 拓扑排序结果（依赖顺序）
    /// </summary>
    public List<EMoModules> TopologicalOrder { get; set; } = [];

    /// <summary>
    /// 模块层级信息（根据依赖深度分层）
    /// </summary>
    public Dictionary<int, List<EMoModules>> ModuleLayers { get; set; } = [];
}

/// <summary>
/// 模块依赖关系图中的节点
/// </summary>
public class ModuleDependencyNode
{
    /// <summary>
    /// 模块枚举
    /// </summary>
    public EMoModules Module { get; set; }

    /// <summary>
    /// 模块名称
    /// </summary>
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>
    /// 模块类型名称
    /// </summary>
    public string ModuleTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 直接依赖的模块数量
    /// </summary>
    public int DirectDependencyCount { get; set; }

    /// <summary>
    /// 所有依赖的模块数量（包括间接依赖）
    /// </summary>
    public int TotalDependencyCount { get; set; }

    /// <summary>
    /// 依赖此模块的模块数量
    /// </summary>
    public int DependentModuleCount { get; set; }

    /// <summary>
    /// 在依赖图中的层级（0表示没有依赖，数字越大表示依赖越多）
    /// </summary>
    public int Layer { get; set; }

    /// <summary>
    /// 是否是循环依赖的一部分
    /// </summary>
    public bool IsPartOfCycle { get; set; }

    /// <summary>
    /// 模块状态
    /// </summary>
    public EMoModuleConfigMethods Status { get; set; }
}

/// <summary>
/// 模块依赖关系图中的边
/// </summary>
public class ModuleDependencyEdge
{
    /// <summary>
    /// 源模块（依赖者）
    /// </summary>
    public EMoModules SourceModule { get; set; }

    /// <summary>
    /// 目标模块（被依赖者）
    /// </summary>
    public EMoModules TargetModule { get; set; }

    /// <summary>
    /// 依赖类型
    /// </summary>
    public DependencyType DependencyType { get; set; }

    /// <summary>
    /// 是否是循环依赖的一部分
    /// </summary>
    public bool IsPartOfCycle { get; set; }
}

/// <summary>
/// 依赖类型
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// 直接依赖
    /// </summary>
    Direct,

    /// <summary>
    /// 间接依赖（传递依赖）
    /// </summary>
    Transitive,

    /// <summary>
    /// 循环依赖
    /// </summary>
    Circular
} 