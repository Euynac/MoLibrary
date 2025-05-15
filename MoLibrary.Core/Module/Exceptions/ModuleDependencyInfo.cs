using System;
using System.Collections.Generic;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Exceptions;

/// <summary>
/// Represents information about module dependencies.
/// </summary>
public class ModuleDependencyInfo
{
    /// <summary>
    /// The module type that depends on other modules.
    /// </summary>
    public Type SourceModuleType { get; set; } = null!;
    
    /// <summary>
    /// List of module types that the source module depends on.
    /// </summary>
    public List<Type> DependedOnModuleTypes { get; set; } = new();
    
    /// <summary>
    /// Dictionary mapping module types to their enum representations.
    /// </summary>
    public Dictionary<Type, EMoModules> ModuleTypeToEnumMap { get; set; } = new();
    
    /// <summary>
    /// Dependency path showing the chain of dependencies that led to an issue.
    /// </summary>
    public List<Type> DependencyPath { get; set; } = new();

    /// <summary>
    /// Creates a formatted string representation of the dependency path.
    /// </summary>
    /// <returns>A string showing the dependency chain.</returns>
    public string GetFormattedDependencyPath()
    {
        if (DependencyPath.Count == 0)
            return "No dependency path available.";

        var pathParts = new List<string>();
        foreach (var moduleType in DependencyPath)
        {
            var moduleName = moduleType.Name;
            if (ModuleTypeToEnumMap.TryGetValue(moduleType, out var moduleEnum))
            {
                moduleName += $" ({moduleEnum})";
            }

            pathParts.Add(moduleName);
        }
        
        return string.Join(" -> ", pathParts);
    }
}