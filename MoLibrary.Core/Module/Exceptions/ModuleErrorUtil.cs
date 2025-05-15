using System.Text;
using MoLibrary.Core.Module.Models;

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
                    MissingConfigKeys = missingKeys
                });
            }
        }
        
        // If there are errors, throw an exception
        if (moduleRegisterErrors.Count > 0)
        {
            throw new ModuleRegisterException(BuildErrorMessage(moduleRegisterErrors));
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
            sb.AppendLine($"Module {group.Key.Name}:");
            
            foreach (var error in group)
            {
                sb.AppendLine($"  - Error: {error.ErrorMessage}");
                
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
                    sb.AppendLine("  - Dependency Path:");
                    sb.AppendLine($"    {error.DependencyInfo.GetFormattedDependencyPath()}");
                }
                
                sb.AppendLine();
            }
        }
        
        return sb.ToString();
    }
} 