using System.Text;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Module.ModuleAnalyser;

namespace MoLibrary.Core.Module.Exceptions;

/// <summary>
/// Utility class for handling module-related errors.
/// </summary>
public static class ModuleErrorUtil
{
    /// <summary>
    /// Validates module configuration requirements and throws an exception if requirements are not met.
    /// </summary>
    /// <param name="moduleRegisterContextDict">Dictionary of module registration contexts.</param>
    /// <param name="moduleRegisterErrors">List to store any registration errors.</param>
    /// <exception cref="ModuleRegisterException">Thrown when module registration requirements are not met.</exception>
    public static void ValidateModuleRequirements(
        Dictionary<Type, ModuleRequestInfo> moduleRegisterContextDict,
        List<ModuleRegisterError> moduleRegisterErrors)
    {
        // Check if modules meet necessary configuration requirements
        foreach (var (moduleType, info) in moduleRegisterContextDict)
        {
            // Check if there are required configuration method keys
            if (info.RequiredConfigMethodKeys.Count == 0) continue;
            
            // Check if each required configuration method key is configured
            var missingKeys = info.GetMissingRequiredConfigMethodKeys();
            
            if (missingKeys.Count > 0)
            {
                moduleRegisterErrors.Add(new ModuleRegisterError
                {
                    ModuleType = moduleType,
                    ErrorMessage = "Missing required configuration",
                    MissingConfigKeys = missingKeys,
                    ErrorType = ModuleRegisterErrorType.MissingRequiredConfig
                });
            }
        }
        
        // Check for missing dependencies
        ValidateDependencies(moduleRegisterErrors);
        
        // If there are errors, throw an exception
        if (moduleRegisterErrors.Count > 0)
        {
            throw new ModuleRegisterException(BuildErrorMessage(moduleRegisterErrors));
        }
    }
    
    /// <summary>
    /// Validates dependencies between modules using the ModuleAnalyser.
    /// </summary>
    /// <param name="moduleRegisterErrors">List to store any dependency-related errors.</param>
    private static void ValidateDependencies(List<ModuleRegisterError> moduleRegisterErrors)
    {
        // Check for circular dependencies
        if (MoModuleAnalyser.HasCircularDependencies())
        {
            // Find modules involved in cycles
            foreach (var module in Enum.GetValues(typeof(EMoModules)).Cast<EMoModules>())
            {
                var dependencyInfo = MoModuleAnalyser.GetModuleDependencyInfo(module);
                
                if (dependencyInfo.IsPartOfCycle)
                {
                    // Get module type from enum
                    Type moduleType = null;
                    foreach (var entry in MoModuleAnalyser.ModuleTypeToEnumMap)
                    {
                        if (entry.Value == module)
                        {
                            moduleType = entry.Key;
                            break;
                        }
                    }
                    
                    // If we can't find the type, skip this error
                    if (moduleType == null) continue;
                    
                    moduleRegisterErrors.Add(new ModuleRegisterError
                    {
                        ModuleType = moduleType,
                        ErrorMessage = $"Module is part of a circular dependency chain: {string.Join(" → ", dependencyInfo.CyclePath)} → {module}",
                        ErrorType = ModuleRegisterErrorType.CircularDependency,
                        DependencyInfo = dependencyInfo
                    });
                }
            }
        }
    }
    
    /// <summary>
    /// Builds a detailed error message from a list of module registration errors.
    /// </summary>
    /// <param name="errors">List of module registration errors.</param>
    /// <returns>A formatted error message.</returns>
    public static string BuildErrorMessage(List<ModuleRegisterError> errors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Module registration errors:");
        
        // Group errors by module
        var groupedErrors = errors.GroupBy(e => e.ModuleType);
        
        foreach (var group in groupedErrors)
        {
            sb.AppendLine($"Module {group.Key?.Name ?? "Unknown"}:");
            
            foreach (var error in group)
            {
                sb.AppendLine($"  - Error Type: {error.ErrorType}");
                sb.AppendLine($"  - Details: {error.ErrorMessage}");
                
                if (error.GuideFrom.HasValue)
                {
                    sb.AppendLine($"  - Source: {error.GuideFrom}");
                }
                
                if (error.MissingConfigKeys.Count > 0)
                {
                    sb.AppendLine("  - Missing configuration methods:");
                    
                    foreach (var key in error.MissingConfigKeys)
                    {
                        sb.AppendLine($"    * {key}");
                    }
                }
                
                if (error.DependencyInfo != null)
                {
                    sb.AppendLine("  - Dependency Information:");
                    sb.AppendLine($"    {error.DependencyInfo}");
                }
                
                sb.AppendLine();
            }
        }
        
        return sb.ToString();
    }
} 