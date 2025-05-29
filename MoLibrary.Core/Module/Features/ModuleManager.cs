using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoLogProvider;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Features;

/// <summary>
/// Manages module state including enabling and disabling modules.
/// </summary>
public static class ModuleManager
{
    public static ILogger Logger { get; set; } = LogProvider.For(typeof(ModuleManager));

    /// <summary>
    /// List of disabled module types
    /// </summary>
    internal static HashSet<Type> DisabledModuleTypes { get; } = new();

    /// <summary>
    /// Gets the list of disabled module types
    /// </summary>
    /// <returns>A list of disabled module types</returns>
    internal static List<Type> GetDisabledModuleTypes()
    {
        return DisabledModuleTypes.ToList();
    }

    /// <summary>
    /// Disables a module due to an exception or configuration
    /// </summary>
    /// <param name="moduleType">The type of module to disable</param>
    /// <returns>True if the module was successfully disabled, false if it was already disabled</returns>
    internal static bool DisableModule(Type moduleType)
    {
        if (!DisabledModuleTypes.Add(moduleType)) return false;
        CascadeDisableModulesThatDependOn(moduleType);
        return true;

    }

    /// <summary>
    /// Checks if a module is disabled
    /// </summary>
    /// <param name="moduleType">The type of module to check</param>
    /// <returns>True if the module is disabled, false otherwise</returns>
    internal static bool IsModuleDisabled(Type moduleType)
    {
        // Check if the module is in the disabled list
        if (DisabledModuleTypes.Contains(moduleType))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Cascade disables modules that depend on the specified module
    /// </summary>
    /// <param name="moduleType">The module type that other modules might depend on</param>
    internal static void CascadeDisableModulesThatDependOn(Type moduleType)
    {
        // Find the module enum for the disabled module
        EMoModules? disabledModuleEnum = null;
        if (ModuleAnalyser.ModuleTypeToEnumMap.TryGetValue(moduleType, out var moduleEnum))
        {
            disabledModuleEnum = moduleEnum;
        }
        
        if (disabledModuleEnum == null) return;
        
        // Get all modules that depend on this module from the dependency map
        var dependentModuleEnums = new HashSet<EMoModules>();
        foreach (var entry in ModuleAnalyser.ModuleDependencyMap)
        {
            if (entry.Value.Contains(disabledModuleEnum.Value))
            {
                dependentModuleEnums.Add(entry.Key);
            }
        }
        
        // Disable all dependent modules
        foreach (var dependentModuleEnum in dependentModuleEnums)
        {
            // Skip if not registered in the enum-to-type map
            if (!ModuleAnalyser.ModuleEnumToTypeDict.TryGetValue(dependentModuleEnum, out var dependentModuleType))
                continue;
            
            if (DisableModule(dependentModuleType))
            {
                Logger.LogWarning(
                    "Module {ModuleName} was disabled because it depends on disabled module {DisabledModuleName}",
                    dependentModuleType.Name,
                    moduleType.Name);
                
                // Recursively cascade disable
                CascadeDisableModulesThatDependOn(dependentModuleType);
            }
        }
    }
    /// <summary>
    /// Initializes the module system by checking for disabled modules and cascading the disable status to dependent modules.
    /// </summary>
    internal static void Init()
    {
        // Check each registered module to see if it's disabled
        foreach (var (moduleType, info) in MoModuleRegisterCentre.ModuleRegisterContextDict)
        {
            if (!info.ModuleOption.IsDisabled) continue;
            DisableModule(moduleType);
            Logger.LogWarning("Module {ModuleName} is disabled by configuration", moduleType.Name);
        }
    }
}