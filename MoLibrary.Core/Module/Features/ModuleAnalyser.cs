using System.Text;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Features;

/// <summary>
/// Provides analysis capabilities for MoModule dependencies and relationships.
/// </summary>
public class ModuleAnalyser
{
    /// <summary>
    /// Dictionary mapping module types to their enum representations.
    /// </summary>
    public static Dictionary<Type, EMoModules> ModuleTypeToEnumMap { get; set; } = new();

    /// <summary>
    /// Dictionary mapping module enums to their type representations.
    /// </summary>
    public static Dictionary<EMoModules, Type> ModuleEnumToTypeDict { get; set; } = new();

    /// <summary>
    /// Dictionary mapping module enums to their dependencies.
    /// </summary>
    public static Dictionary<EMoModules, HashSet<EMoModules>> ModuleDependencyMap { get; set; } = new();

    /// <summary>
    /// Maps a module enum to its type.
    /// </summary>
    /// <param name="moduleType">The module type.</param>
    /// <param name="moduleEnum">The module enum.</param>
    public static void RegisterModuleMapping(Type moduleType, EMoModules moduleEnum)
    {
        ModuleTypeToEnumMap[moduleType] = moduleEnum;
        ModuleEnumToTypeDict[moduleEnum] = moduleType;
    }

    /// <summary>
    /// Adds a dependency relationship between modules.
    /// </summary>
    /// <param name="moduleEnum">The module that depends on another module.</param>
    /// <param name="dependsOnEnum">The module being depended upon.</param>
    public static void AddDependency(EMoModules moduleEnum, EMoModules dependsOnEnum)
    {
        if (!ModuleDependencyMap.ContainsKey(moduleEnum))
        {
            ModuleDependencyMap[moduleEnum] = [];
        }
        
        if (moduleEnum != dependsOnEnum) // Prevent self-dependency
        {
            ModuleDependencyMap[moduleEnum].Add(dependsOnEnum);
        }
    }

    /// <summary>
    /// 刷新模块注册顺序，确保依赖的模块优先注册。
    /// </summary>
    public static void RefreshModuleOrders()
    {
        // 获取拓扑排序的模块顺序
        var orderedModules = GetModulesInDependencyOrder();
        orderedModules.Reverse();
        
        // 为每个模块分配Order值，依赖的模块获得更小的Order值
        for (int i = 0; i < orderedModules.Count; i++)
        {
            var moduleEnum = orderedModules[i];
            
            // 查找对应的模块类型
            if (ModuleEnumToTypeDict.TryGetValue(moduleEnum, out var moduleType))
            {
                // 查找模块注册上下文并更新Order
                if (MoModuleRegisterCentre.TryGetModuleRequestInfo(moduleType, out var requestInfo))
                {
                    // Order值从100开始，每个模块递增10，确保有足够的间隔
                    requestInfo.Order = 100 + (i * 10);
                }
            }
        }
    }

    /// <summary>
    /// 手动刷新所有已注册模块的注册顺序。
    /// 通常在所有模块依赖关系建立完成后调用，确保模块按正确的依赖顺序注册。
    /// </summary>
    public static void RefreshAllModuleOrders()
    {
        //// 检查是否有循环依赖
        //if (HasCircularDependencies())
        //{
        //    throw new InvalidOperationException("检测到模块间存在循环依赖，无法确定正确的注册顺序。请检查模块依赖关系。");
        //}
        
        RefreshModuleOrders();
    }

    /// <summary>
    /// Calculates all dependencies for a specific module, including transitive dependencies.
    /// </summary>
    /// <param name="moduleEnum">The module to calculate dependencies for.</param>
    /// <returns>A set of all direct and indirect dependencies of the module.</returns>
    public static HashSet<EMoModules> CalculateModuleDependencies(EMoModules moduleEnum)
    {
        var allDependencies = new HashSet<EMoModules>();
        if (!ModuleDependencyMap.ContainsKey(moduleEnum))
        {
            return allDependencies;
        }

        var visited = new HashSet<EMoModules>();
        var toVisit = new Queue<EMoModules>();
        
        // Start with direct dependencies
        foreach (var dependency in ModuleDependencyMap[moduleEnum])
        {
            toVisit.Enqueue(dependency);
        }
        
        // Process the dependency graph breadth-first
        while (toVisit.Count > 0)
        {
            var current = toVisit.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            allDependencies.Add(current);
            
            // Add dependencies of the current module if any
            if (ModuleDependencyMap.TryGetValue(current, out var dependencies))
            {
                foreach (var dependency in dependencies.Where(d => !visited.Contains(d)))
                {
                    toVisit.Enqueue(dependency);
                }
            }
        }
        
        return allDependencies;
    }

    /// <summary>
    /// Calculates the complete dependency graph for all modules.
    /// </summary>
    /// <returns>A DirectedGraph representation of the module dependencies.</returns>
    public static DirectedGraph<EMoModules> CalculateCompleteModuleDependencyGraph()
    {
        var graph = new DirectedGraph<EMoModules>();
        
        // Add all modules as nodes
        foreach (var module in ModuleEnumToTypeDict.Keys)
        {
            graph.AddNode(module);
        }
        
        // Add all edges (dependencies)
        foreach (var kvp in ModuleDependencyMap)
        {
            var sourceModule = kvp.Key;
            foreach (var targetModule in kvp.Value)
            {
                graph.AddEdge(sourceModule, targetModule);
            }
        }
        
        return graph;
    }

    /// <summary>
    /// Detects if there are any circular dependencies in the module dependencies.
    /// </summary>
    /// <returns>True if circular dependencies exist, otherwise false.</returns>
    public static bool HasCircularDependencies()
    {
        var graph = CalculateCompleteModuleDependencyGraph();
        return graph.HasCycles();
    }

    /// <summary>
    /// Gets a topological sort of modules based on their dependencies.
    /// </summary>
    /// <returns>A list of modules in dependency order (if no cycles exist).</returns>
    public static List<EMoModules> GetModulesInDependencyOrder()
    {
        var graph = CalculateCompleteModuleDependencyGraph();
        return graph.TopologicalSort();
    }
    
    /// <summary>
    /// Gets detailed information about a module's dependencies.
    /// </summary>
    /// <param name="moduleEnum">The module to analyze.</param>
    /// <returns>Detailed dependency information for the module.</returns>
    public static ModuleDependencyInfo GetModuleDependencyInfo(EMoModules moduleEnum)
    {
        var info = new ModuleDependencyInfo
        {
            Module = moduleEnum
        };
        
        // Get direct dependencies
        if (ModuleDependencyMap.TryGetValue(moduleEnum, out var directDeps))
        {
            info.DirectDependencies = [..directDeps];
        }
        
        // Get all dependencies
        info.AllDependencies = CalculateModuleDependencies(moduleEnum);
        
        // Get modules that depend on this module
        foreach (var kvp in ModuleDependencyMap)
        {
            if (kvp.Value.Contains(moduleEnum))
            {
                info.DependedByModules.Add(kvp.Key);
            }
        }
        
        // Check for cycles involving this module
        var cyclePath = FindCycleInvolvingModule(moduleEnum);
        if (cyclePath.Count > 0)
        {
            info.IsPartOfCycle = true;
            info.CyclePath = cyclePath;
        }
        
        return info;
    }
    
    /// <summary>
    /// Finds a cycle in the dependency graph that involves the specified module.
    /// </summary>
    /// <param name="moduleEnum">The module to check for involvement in a cycle.</param>
    /// <returns>A list representing the cycle path, or an empty list if no cycle exists.</returns>
    public static List<EMoModules> FindCycleInvolvingModule(EMoModules moduleEnum)
    {
        if (!ModuleDependencyMap.ContainsKey(moduleEnum))
        {
            return [];
        }
        
        var visited = new HashSet<EMoModules>();
        var path = new List<EMoModules>();
        var inPath = new HashSet<EMoModules>();
        
        bool DFS(EMoModules current)
        {
            if (inPath.Contains(current))
            {
                // Found a cycle - collect the path
                int cycleStart = path.IndexOf(current);
                return true;
            }
            
            if (!visited.Add(current))
            {
                return false;
            }

            inPath.Add(current);
            path.Add(current);
            
            if (ModuleDependencyMap.TryGetValue(current, out var dependencies))
            {
                foreach (var dependency in dependencies)
                {
                    if (DFS(dependency))
                    {
                        return true;
                    }
                }
            }
            
            inPath.Remove(current);
            path.RemoveAt(path.Count - 1);
            return false;
        }
        
        // Start DFS from the module we're interested in
        DFS(moduleEnum);
        
        // Extract the cycle path if one was found
        var cyclePath = new List<EMoModules>();
        for (int i = 0; i < path.Count; i++)
        {
            if (path[i] == moduleEnum)
            {
                var cycleStart = i;
                for (int j = cycleStart; j < path.Count; j++)
                {
                    cyclePath.Add(path[j]);
                }
                break;
            }
        }
        
        return cyclePath;
    }
    
    /// <summary>
    /// Gets dependency information for all modules.
    /// </summary>
    /// <returns>A dictionary mapping each module to its dependency information.</returns>
    public static Dictionary<EMoModules, ModuleDependencyInfo> GetAllModuleDependencyInfo()
    {
        var result = new Dictionary<EMoModules, ModuleDependencyInfo>();
        
        foreach (var module in Enum.GetValues(typeof(EMoModules)).Cast<EMoModules>())
        {
            result[module] = GetModuleDependencyInfo(module);
        }
        
        return result;
    }

    /// <summary>
    /// 获取所有已注册模块的当前注册顺序信息。
    /// </summary>
    /// <returns>包含模块类型、枚举值和注册顺序的字典</returns>
    public static Dictionary<Type, (EMoModules ModuleEnum, int Order)> GetModuleRegistrationOrder()
    {
        var result = new Dictionary<Type, (EMoModules, int)>();
        
        foreach (var kvp in MoModuleRegisterCentre.ModuleRegisterContextDict)
        {
            var moduleType = kvp.Key;
            var requestInfo = kvp.Value;
            
            // 查找对应的模块枚举
            if (ModuleTypeToEnumMap.TryGetValue(moduleType, out var moduleEnum))
            {
                result[moduleType] = (moduleEnum, requestInfo.Order);
            }
            else
            {
                // 如果没有找到对应的枚举，使用Developer作为默认值
                result[moduleType] = (EMoModules.Developer, requestInfo.Order);
            }
        }
        
        return result;
    }

    /// <summary>
    /// 获取模块注册状况的格式化字符串，用于调试输出。
    /// </summary>
    /// <returns>包含模块注册状况、依赖关系、禁用状态和初始化耗时的格式化字符串</returns>
    public static string GetModuleRegistrationSummary()
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("Module Registration Summary:");
        sb.AppendLine("=====================================");
        
        // 从 ModuleSnapshots 获取信息并按 Order 排序（这些都是启用的模块）
        var moduleInfos = MoModuleRegisterCentre.ModuleSnapshots
            .OrderBy(snapshot => snapshot.RegisterInfo.Order)
            .ToList();
        
        // 从 ModuleManager 获取所有禁用的模块类型
        var disabledModuleTypes = ModuleManager.GetDisabledModuleTypes();
        
        // 显示启用的模块
        if (moduleInfos.Count > 0)
        {
            sb.AppendLine("Enabled Modules:");
            sb.AppendLine("----------------");
            
            foreach (var snapshot in moduleInfos)
            {
                var moduleEnum = snapshot.ModuleEnum;
                var order = snapshot.RegisterInfo.Order;
                var moduleTypeName = snapshot.ModuleType.Name;
                var initDuration = snapshot.TotalInitializationDurationMs;
                
                // 显示模块基本信息
                sb.AppendLine($"Order {order:D4}: {moduleEnum} ({moduleTypeName})");
                
                // 显示依赖关系
                if (ModuleDependencyMap.TryGetValue(moduleEnum, out var dependencies) && dependencies.Count > 0)
                {
                    sb.AppendLine($"           Dependencies: {string.Join(", ", dependencies)}");
                }

                // 显示初始化耗时
                sb.AppendLine($"           Initialization Time: {initDuration}ms");
            }
        }
        else
        {
            sb.AppendLine("No enabled modules found.");
        }
        
        // 显示禁用的模块
        if (disabledModuleTypes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Disabled Modules:");
            sb.AppendLine("-----------------");
            
            foreach (var disabledModuleType in disabledModuleTypes)
            {
                // 获取模块枚举值
                var moduleEnum = ModuleTypeToEnumMap.TryGetValue(disabledModuleType, out var enumValue) 
                    ? enumValue 
                    : EMoModules.Developer;
                
                sb.AppendLine($"{moduleEnum} ({disabledModuleType.Name}) [DISABLED]");
                
                // 显示依赖关系（如果有的话）
                if (ModuleDependencyMap.TryGetValue(moduleEnum, out var dependencies) && dependencies.Count > 0)
                {
                    sb.AppendLine($"           Dependencies: {string.Join(", ", dependencies)}");
                }
            }
        }
        
        // 添加统计信息
        var totalEnabledModules = moduleInfos.Count;
        var totalDisabledModules = disabledModuleTypes.Count;
        var totalModules = totalEnabledModules + totalDisabledModules;
        var totalInitTime = moduleInfos.Sum(s => s.TotalInitializationDurationMs);
        
        sb.AppendLine();
        sb.AppendLine("Statistics:");
        sb.AppendLine("===========");
        sb.AppendLine($"  Total modules: {totalModules}");
        sb.AppendLine($"  Enabled modules: {totalEnabledModules}");
        sb.AppendLine($"  Disabled modules: {totalDisabledModules}");
        sb.AppendLine($"  Total initialization time: {totalInitTime}ms");
        
        // 显示耗时最多的前5个模块（只显示启用的模块）
        var slowestModules = moduleInfos
            .Where(s => s.TotalInitializationDurationMs > 0)
            .OrderByDescending(s => s.TotalInitializationDurationMs)
            .Take(5)
            .ToList();
            
        if (slowestModules.Count > 0)
        {
            sb.AppendLine($"  Slowest modules:");
            foreach (var module in slowestModules)
            {
                sb.AppendLine($"    {module.ModuleEnum}: {module.TotalInitializationDurationMs}ms");
            }
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// Represents a directed graph data structure for module dependency analysis.
/// </summary>
/// <typeparam name="T">The type of nodes in the graph.</typeparam>
public class DirectedGraph<T> where T : notnull
{
    private readonly Dictionary<T, HashSet<T>> _adjacencyList = new();
    
    /// <summary>
    /// Adds a node to the graph.
    /// </summary>
    /// <param name="node">The node to add.</param>
    public void AddNode(T node)
    {
        if (!_adjacencyList.ContainsKey(node))
        {
            _adjacencyList[node] = [];
        }
    }
    
    /// <summary>
    /// Adds a directed edge from source to target.
    /// </summary>
    /// <param name="source">The source node.</param>
    /// <param name="target">The target node.</param>
    public void AddEdge(T source, T target)
    {
        AddNode(source);
        AddNode(target);
        _adjacencyList[source].Add(target);
    }
    
    /// <summary>
    /// Checks if the graph has any cycles.
    /// </summary>
    /// <returns>True if cycles exist, otherwise false.</returns>
    public bool HasCycles()
    {
        var visited = new HashSet<T>();
        var recursionStack = new HashSet<T>();
        
        foreach (var node in _adjacencyList.Keys)
        {
            if (HasCyclesDFS(node, visited, recursionStack))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private bool HasCyclesDFS(T node, HashSet<T> visited, HashSet<T> recursionStack)
    {
        if (recursionStack.Contains(node))
        {
            return true;
        }
        
        if (!visited.Add(node))
        {
            return false;
        }

        recursionStack.Add(node);
        
        if (_adjacencyList.TryGetValue(node, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (HasCyclesDFS(neighbor, visited, recursionStack))
                {
                    return true;
                }
            }
        }
        
        recursionStack.Remove(node);
        return false;
    }
    
    /// <summary>
    /// Performs a topological sort of the graph.
    /// </summary>
    /// <returns>A list of nodes in topological order (if no cycles exist).</returns>
    public List<T> TopologicalSort()
    {
        var result = new List<T>();
        var visited = new HashSet<T>();
        var temp = new HashSet<T>();
        
        foreach (var node in _adjacencyList.Keys)
        {
            if (!visited.Contains(node) && !temp.Contains(node))
            {
                TopologicalSortDFS(node, visited, temp, result);
            }
        }
        
        result.Reverse();
        return result;
    }
    
    private void TopologicalSortDFS(T node, HashSet<T> visited, HashSet<T> temp, List<T> result)
    {
        temp.Add(node);
        
        if (_adjacencyList.TryGetValue(node, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (temp.Contains(neighbor))
                {
                    // Cycle detected
                    continue;
                }
                
                if (!visited.Contains(neighbor))
                {
                    TopologicalSortDFS(neighbor, visited, temp, result);
                }
            }
        }
        
        temp.Remove(node);
        visited.Add(node);
        result.Add(node);
    }
    
    /// <summary>
    /// Returns a string representation of the graph.
    /// </summary>
    /// <returns>A formatted string showing the graph structure.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Module Dependency Graph:");
        
        foreach (var node in _adjacencyList.Keys.OrderBy(n => n.ToString()))
        {
            sb.Append($"{node} -> ");
            
            if (_adjacencyList[node].Count == 0)
            {
                sb.AppendLine("(no dependencies)");
            }
            else
            {
                sb.AppendLine(string.Join(", ", _adjacencyList[node].OrderBy(n => n.ToString())));
            }
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Gets all nodes in the graph.
    /// </summary>
    public IEnumerable<T> Nodes => _adjacencyList.Keys;
    
    /// <summary>
    /// Gets all edges in the graph as tuples (source, target).
    /// </summary>
    public IEnumerable<(T Source, T Target)> Edges
    {
        get
        {
            foreach (var source in _adjacencyList.Keys)
            {
                foreach (var target in _adjacencyList[source])
                {
                    yield return (source, target);
                }
            }
        }
    }
}