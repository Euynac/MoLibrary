using System.Collections.Frozen;
using System.Reflection;
using System.Text;
using Monica.Core.Modularity.State;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Provides analysis capabilities for ModuleBase dependencies and relationships.
/// </summary>
public sealed class ModuleDependencyAnalyzer(MonicaApplication application)
{
    private readonly ModuleDependencyState _state = new();

    /// <summary>
    /// Clears the dependency graph owned by this host.
    /// </summary>
    internal void Clear()
    {
        _state.Clear();
    }

    /// <summary>
    /// Gets a point-in-time, read-only snapshot mapping module types to their declared keys.
    /// </summary>
    public IReadOnlyDictionary<Type, ModuleKey> ModuleKeysByType =>
        _state.CreateModuleKeysByTypeSnapshot();

    /// <summary>
    /// Gets a point-in-time, read-only snapshot mapping module keys to their types.
    /// </summary>
    public IReadOnlyDictionary<ModuleKey, Type> ModuleTypesByKey =>
        _state.CreateModuleTypesByKeySnapshot();

    /// <summary>
    /// Gets a point-in-time, deeply read-only snapshot of the module dependency graph.
    /// </summary>
    public IReadOnlyDictionary<ModuleKey, IReadOnlySet<ModuleKey>> DependenciesByModule =>
        _state.CreateDependenciesByModuleSnapshot();

    /// <summary>
    /// Maps a module key to its type.
    /// </summary>
    /// <param name="moduleType">The module type.</param>
    /// <param name="moduleKey">The module key.</param>
    internal void RegisterModuleMapping(Type moduleType, ModuleKey moduleKey)
    {
        _state.RegisterMapping(moduleType, moduleKey);
    }

    /// <summary>
    /// Resolves the module key declared on a module type and caches the type-to-key mapping.
    /// </summary>
    /// <param name="moduleType">The module type.</param>
    /// <returns>The resolved module key.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the module type has no <see cref="ModuleKeyAttribute"/>.</exception>
    internal ModuleKey ResolveModuleKey(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);

        if (_state.TryGetModuleKey(moduleType, out var cached))
        {
            return cached;
        }

        var attribute = moduleType.GetCustomAttribute<ModuleKeyAttribute>()
            ?? throw new InvalidOperationException($"Module {moduleType.Name} has no [ModuleKey] attribute.");

        RegisterModuleMapping(moduleType, attribute.Key);
        return attribute.Key;
    }

    /// <summary>
    /// Adds a dependency relationship between modules.
    /// </summary>
    /// <param name="moduleKey">The module that depends on another module.</param>
    /// <param name="dependsOnKey">The module being depended upon.</param>
    internal void AddDependency(ModuleKey moduleKey, ModuleKey dependsOnKey)
    {
        _state.AddDependency(moduleKey, dependsOnKey);
    }

    /// <summary>
    /// Refreshes module registration order so dependencies are registered first.
    /// </summary>
    private void RefreshModuleOrders()
    {
        // Get modules in topological dependency order.
        var orderedModules = GetModulesInDependencyOrder().Reverse().ToList();

        // Assign smaller order values to modules that other modules depend on.
        for (int i = 0; i < orderedModules.Count; i++)
        {
            var moduleKey = orderedModules[i];

            // Resolve the module type for this key.
            if (_state.TryGetModuleType(moduleKey, out var moduleType))
            {
                // Update the stored registration order.
                if (application.Modules.TryGetModuleRequestInfo(moduleType, out var requestInfo))
                {
                    // Start at 100 and leave gaps of 10 to make later adjustments easier.
                    requestInfo.Order = 100 + (i * 10);
                }
            }
        }
    }

    /// <summary>
    /// Manually refreshes registration order for all registered modules.
    /// Call this after dependency discovery completes so modules register in the correct dependency order.
    /// </summary>
    internal void RefreshAllModuleOrders()
    {
        //// Check for circular dependencies.
        //if (HasCircularDependencies())
        //{
        //    throw new InvalidOperationException("Circular dependencies were detected between modules. The correct registration order cannot be determined. Check the module dependency graph.");
        //}
        
        RefreshModuleOrders();
    }

    /// <summary>
    /// Calculates all dependencies for a specific module, including transitive dependencies.
    /// </summary>
    /// <param name="moduleKey">The module to calculate dependencies for.</param>
    /// <returns>A set of all direct and indirect dependencies of the module.</returns>
    public IReadOnlySet<ModuleKey> CalculateModuleDependencies(ModuleKey moduleKey)
    {
        var allDependencies = new HashSet<ModuleKey>();
        if (!_state.TryGetDependencies(moduleKey, out var directDependencies))
        {
            return FrozenSet<ModuleKey>.Empty;
        }

        var visited = new HashSet<ModuleKey> { moduleKey };
        var toVisit = new Queue<ModuleKey>();

        // Start with direct dependencies
        foreach (var dependency in directDependencies)
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
            if (_state.TryGetDependencies(current, out var dependencies))
            {
                foreach (var dependency in dependencies.Where(d => !visited.Contains(d)))
                {
                    toVisit.Enqueue(dependency);
                }
            }
        }

        return allDependencies.ToFrozenSet();
    }

    /// <summary>
    /// Calculates the complete dependency graph for all modules.
    /// </summary>
    /// <returns>A DirectedGraph representation of the module dependencies.</returns>
    public DirectedGraph<ModuleKey> CalculateCompleteModuleDependencyGraph()
    {
        var graph = new DirectedGraph<ModuleKey>();

        // Add all registered modules as nodes, even if they have no dependencies.
        foreach (var moduleType in application.Modules.Registrations.Keys)
        {
            graph.AddNode(ResolveModuleKey(moduleType));
        }

        // Preserve any additional mappings that may have been populated outside normal registration.
        foreach (var module in _state.MappedModuleKeys)
        {
            graph.AddNode(module);
        }

        // Add all edges (dependencies)
        foreach (var (sourceModule, dependencies) in _state.DependencyEntries)
        {
            foreach (var targetModule in dependencies)
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
    public bool HasCircularDependencies()
    {
        var graph = CalculateCompleteModuleDependencyGraph();
        return graph.HasCycles();
    }

    /// <summary>
    /// Gets a topological sort of modules based on their dependencies.
    /// </summary>
    /// <returns>A list of modules in dependency order (if no cycles exist).</returns>
    public IReadOnlyList<ModuleKey> GetModulesInDependencyOrder()
    {
        var graph = CalculateCompleteModuleDependencyGraph();
        return Array.AsReadOnly(graph.TopologicalSort().ToArray());
    }
    
    /// <summary>
    /// Gets detailed information about a module's dependencies.
    /// </summary>
    /// <param name="moduleKey">The module to analyze.</param>
    /// <returns>Detailed dependency information for the module.</returns>
    public ModuleDependencyInfo GetModuleDependencyInfo(ModuleKey moduleKey)
    {
        var info = new ModuleDependencyInfo
        {
            Module = moduleKey
        };

        // Get direct dependencies
        if (_state.TryGetDependencies(moduleKey, out var directDeps))
        {
            info.DirectDependencies = [..directDeps];
        }

        // Get all dependencies
        info.AllDependencies = [.. CalculateModuleDependencies(moduleKey)];

        // Get modules that depend on this module
        foreach (var (dependentModule, dependencies) in _state.DependencyEntries)
        {
            if (dependencies.Contains(moduleKey))
            {
                info.DependedByModules.Add(dependentModule);
            }
        }

        // Check for cycles involving this module
        var cyclePath = FindCycleInvolvingModule(moduleKey);
        if (cyclePath.Count > 0)
        {
            info.IsPartOfCycle = true;
            info.CyclePath = [.. cyclePath];
        }

        return info;
    }
    
    /// <summary>
    /// Finds a cycle in the dependency graph that involves the specified module.
    /// </summary>
    /// <param name="moduleKey">The module to check for involvement in a cycle.</param>
    /// <returns>A list representing the cycle path, or an empty list if no cycle exists.</returns>
    public IReadOnlyList<ModuleKey> FindCycleInvolvingModule(ModuleKey moduleKey)
    {
        if (!_state.TryGetDependencies(moduleKey, out _))
        {
            return Array.Empty<ModuleKey>();
        }

        var visited = new HashSet<ModuleKey>();
        var path = new List<ModuleKey>();
        var inPath = new HashSet<ModuleKey>();

        bool DFS(ModuleKey current)
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

            if (_state.TryGetDependencies(current, out var dependencies))
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
        DFS(moduleKey);

        // Extract the cycle path if one was found
        var cyclePath = new List<ModuleKey>();
        for (int i = 0; i < path.Count; i++)
        {
            if (path[i] == moduleKey)
            {
                var cycleStart = i;
                for (int j = cycleStart; j < path.Count; j++)
                {
                    cyclePath.Add(path[j]);
                }
                break;
            }
        }

        return Array.AsReadOnly(cyclePath.ToArray());
    }
    
    /// <summary>
    /// Gets dependency information for all registered modules.
    /// </summary>
    /// <returns>A dictionary mapping each module to its dependency information.</returns>
    public IReadOnlyDictionary<ModuleKey, ModuleDependencyInfo> GetAllModuleDependencyInfo()
    {
        var result = new Dictionary<ModuleKey, ModuleDependencyInfo>();

        foreach (var moduleKey in _state.MappedModuleKeys)
        {
            result[moduleKey] = GetModuleDependencyInfo(moduleKey);
        }

        return result.ToFrozenDictionary();
    }

    /// <summary>
    /// Gets the current registration order information for all registered modules.
    /// </summary>
    /// <returns>A dictionary containing the module type, module key, and registration order.</returns>
    public IReadOnlyDictionary<Type, (ModuleKey ModuleKey, int Order)> GetModuleRegistrationOrder()
    {
        var result = new Dictionary<Type, (ModuleKey ModuleKey, int Order)>();

        foreach (var kvp in application.Modules.Registrations)
        {
            var moduleType = kvp.Key;
            var requestInfo = kvp.Value;
            result[moduleType] = (ResolveModuleKey(moduleType), requestInfo.Order);
        }

        return result.ToFrozenDictionary();
    }

    /// <summary>
    /// Builds a formatted registration summary string for debugging output.
    /// </summary>
    /// <returns>A formatted string containing registration state, dependencies, disabled modules, and initialization timings.</returns>
    public string GetModuleRegistrationSummary()
    {
        var sb = new StringBuilder();

        sb.AppendLine("Module Registration Summary:");
        sb.AppendLine("=====================================");

        // Enabled modules come from the runtime snapshots and are already initialized.
        var moduleInfos = application.Modules.RuntimeSnapshots
            .OrderBy(snapshot => snapshot.RegisterInfo.Order)
            .ToList();

        // Pull disabled module types from the module manager.
        var disabledModuleTypes = application.ModuleStates.GetDisabledModuleTypes();

        // Render enabled modules.
        if (moduleInfos.Count > 0)
        {
            sb.AppendLine("Enabled Modules:");
            sb.AppendLine("----------------");

            foreach (var snapshot in moduleInfos)
            {
                var moduleKey = snapshot.ModuleKey;
                var order = snapshot.RegisterInfo.Order;
                var moduleTypeName = snapshot.ModuleType.Name;
                var serialPhaseDuration = snapshot.SerialPhaseDurationMs;

                // Basic module information.
                var moduleKeyDisplay = moduleKey.ToString();
                sb.AppendLine($"Order {order:D4}: {moduleKeyDisplay} ({moduleTypeName})");

                // Dependency information.
                if (_state.TryGetDependencies(moduleKey, out var dependencies) && dependencies.Count > 0)
                {
                    sb.AppendLine($"           Dependencies: {string.Join(", ", dependencies)}");
                }

                // Serial composition callback timing.
                sb.AppendLine($"           Serial Phase Duration: {serialPhaseDuration}ms");
            }
        }
        else
        {
            sb.AppendLine("No enabled modules found.");
        }

        // Render disabled modules.
        if (disabledModuleTypes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Disabled Modules:");
            sb.AppendLine("-----------------");

            foreach (var disabledModuleType in disabledModuleTypes)
            {
                // Resolve the module key for display.
                var moduleKey = ResolveModuleKey(disabledModuleType);
                var moduleKeyDisplay = moduleKey.ToString();

                sb.AppendLine($"{moduleKeyDisplay} ({disabledModuleType.Name}) [DISABLED]");

                // Dependency information, if any.
                if (_state.TryGetDependencies(moduleKey, out var dependencies) && dependencies.Count > 0)
                {
                    sb.AppendLine($"           Dependencies: {string.Join(", ", dependencies)}");
                }
            }
        }

        // Append summary statistics.
        var totalEnabledModules = moduleInfos.Count;
        var totalDisabledModules = disabledModuleTypes.Count;
        var totalModules = totalEnabledModules + totalDisabledModules;
        var totalSerialPhaseDuration = moduleInfos.Sum(s => s.SerialPhaseDurationMs);

        sb.AppendLine();
        sb.AppendLine("Statistics:");
        sb.AppendLine("===========");
        sb.AppendLine($"  Total modules: {totalModules}");
        sb.AppendLine($"  Enabled modules: {totalEnabledModules}");
        sb.AppendLine($"  Disabled modules: {totalDisabledModules}");
        sb.AppendLine($"  Total serial phase duration: {totalSerialPhaseDuration}ms");

        // Show the five slowest enabled modules.
        var slowestModules = moduleInfos
            .Where(s => s.SerialPhaseDurationMs > 0)
            .OrderByDescending(s => s.SerialPhaseDurationMs)
            .Take(5)
            .ToList();

        if (slowestModules.Count > 0)
        {
            sb.AppendLine($"  Slowest modules:");
            foreach (var module in slowestModules)
            {
                var moduleKeyDisplay = module.ModuleKey.ToString();
                sb.AppendLine($"    {moduleKeyDisplay}: {module.SerialPhaseDurationMs}ms");
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
