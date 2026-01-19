using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Features;

/// <summary>
/// Contains information about module dependencies for analysis and error reporting.
/// </summary>
public class ModuleDependencyInfo
{
    /// <summary>
    /// The module being analyzed.
    /// </summary>
    public ModuleKey Module { get; set; }

    /// <summary>
    /// Direct dependencies of the module.
    /// </summary>
    public HashSet<ModuleKey> DirectDependencies { get; set; } = new();

    /// <summary>
    /// All dependencies of the module (direct and transitive).
    /// </summary>
    public HashSet<ModuleKey> AllDependencies { get; set; } = new();

    /// <summary>
    /// A list of modules that depend on this module.
    /// </summary>
    public HashSet<ModuleKey> DependedByModules { get; set; } = new();

    /// <summary>
    /// Information about the dependency path that formed a cycle, if any.
    /// </summary>
    public List<ModuleKey> CyclePath { get; set; } = new();

    /// <summary>
    /// Indicates whether this module is part of a dependency cycle.
    /// </summary>
    public bool IsPartOfCycle { get; set; }

    /// <summary>
    /// Creates a formatted string description of the dependencies.
    /// </summary>
    /// <returns>A string describing the module dependencies.</returns>
    public override string ToString()
    {
        var result = $"Module {Module} dependencies:";

        if (DirectDependencies.Count > 0)
        {
            result += $"\n  Direct dependencies: {string.Join(", ", DirectDependencies)}";
        }
        else
        {
            result += "\n  No direct dependencies";
        }

        if (AllDependencies.Count > DirectDependencies.Count)
        {
            var transitive = new HashSet<ModuleKey>(AllDependencies);
            transitive.ExceptWith(DirectDependencies);
            result += $"\n  Transitive dependencies: {string.Join(", ", transitive)}";
        }

        if (DependedByModules.Count > 0)
        {
            result += $"\n  Depended on by: {string.Join(", ", DependedByModules)}";
        }

        if (IsPartOfCycle && CyclePath.Count > 0)
        {
            result += $"\n  Part of dependency cycle: {string.Join(" → ", CyclePath)} → {Module}";
        }

        return result;
    }
} 