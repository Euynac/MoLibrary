using System.Text;
using MoLibrary.Core.Module.Models;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module.BuilderWrapper;
using MoLibrary.Core.Module.Features;

namespace MoLibrary.Core.Module.Exceptions;

/// <summary>
/// Utility class for handling module-related errors.
/// </summary>
public static class ModuleErrorUtil
{

    /// <summary>
    /// 模块注册错误列表
    /// </summary>
    public static List<ModuleRegisterError> ModuleRegisterErrors { get; } = [];
    /// <summary>
    /// Validates module configuration requirements and throws an exception if requirements are not met.
    /// </summary>
    /// <param name="moduleRegisterContextDict">Dictionary of module registration contexts.</param>
    /// <exception cref="ModuleRegisterException">Thrown when module registration requirements are not met.</exception>
    public static void ValidateModuleRequirements(
        Dictionary<Type, ModuleRequestInfo> moduleRegisterContextDict)
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
                var error = new ModuleRegisterError
                {
                    ModuleType = moduleType,
                    ErrorMessage = "Missing required configuration",
                    MissingConfigKeys = missingKeys,
                    ErrorType = ModuleRegisterErrorType.MissingRequiredConfig
                };
                if(!CheckDisableModuleIfHasException(moduleType, error)) ModuleRegisterErrors.Add(error);
            }

        }
        
        //// Check for missing dependencies
        //ValidateDependencies();
        
        

        // If there are errors, throw an exception
        if (ModuleRegisterErrors.Count > 0)
        {
            throw new ModuleRegisterException(BuildErrorMessage(ModuleRegisterErrors));
        }
    }
    //// 1.1 Check for circular dependencies in the dependency graph
    //if (MoModuleAnalyser.HasCircularDependencies())
    //{
    //    var error = new ModuleRegisterError
    //    {
    //        ErrorMessage = "Circular dependency detected in module dependencies. Please check the dependency graph.",
    //        ErrorType = ModuleRegisterErrorType.CircularDependency
    //    };
    //    ModuleRegisterErrors.Add(error);

    //    // Log the dependency graph for debugging
    //    var graph = MoModuleAnalyser.CalculateCompleteModuleDependencyGraph();

    //   Logger.LogError("Module dependency graph contains circular dependencies:\n{Graph}", graph.ToString());

    //    // Continue with registration but warn about potential issues
    //}
    //else
    //{
    //    var modulesInOrder = MoModuleAnalyser.GetModulesInDependencyOrder();

    //    var graph = MoModuleAnalyser.CalculateCompleteModuleDependencyGraph();
    //    Logger.LogInformation("Modules will be initialized in the following dependency order: {Modules}\n{Graph}",
    //        string.Join(", ", modulesInOrder), graph);
    //}

    /// <summary>
    /// Validates dependencies between modules using the ModuleAnalyser.
    /// </summary>
    private static void ValidateDependencies()
    {
        // Check for circular dependencies
        if (ModuleAnalyser.HasCircularDependencies())
        {
            // Find modules involved in cycles
            foreach (var module in Enum.GetValues(typeof(EMoModules)).Cast<EMoModules>())
            {
                var dependencyInfo = ModuleAnalyser.GetModuleDependencyInfo(module);
                
                if (dependencyInfo.IsPartOfCycle)
                {
                    // Get module type from enum using the direct mapping
                    if (!ModuleAnalyser.ModuleEnumToTypeDict.TryGetValue(module, out var moduleType) || moduleType == null)
                        continue;
                    
                    ModuleRegisterErrors.Add(new ModuleRegisterError
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
    /// Records an error that occurred during module configuration.
    /// </summary>
    /// <param name="moduleType">The type of the module where the error occurred.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="phase">The phase where the error occurred.</param>
    /// <param name="errorType">The type of error.</param>
    public static void RecordModuleError(
        Type moduleType, 
        string? errorMessage, 
        EMoModuleConfigMethods phase, 
        ModuleRegisterErrorType errorType)
    {
        var error = new ModuleRegisterError 
        {
            ModuleType = moduleType,
            ErrorMessage = $"Error in {phase}: {errorMessage}",
            ErrorType = errorType,
            Phase = phase
        };
        ModuleRegisterErrors.Add(error);
        
        if(phase > EMoModuleConfigMethods.ClaimDependencies)
        {
            // Immediately check if the module should be disabled due to exception
            CheckDisableModuleIfHasException(moduleType, error);
        }

    }
    
    /// <summary>
    /// Records an error that occurred during a module request.
    /// </summary>
    /// <param name="moduleType">The type of the module where the error occurred.</param>
    /// <param name="request">The request that caused the error.</param>
    /// <param name="exception">The exception that was thrown.</param>
    public static void RecordRequestError(
        Type moduleType, 
        ModuleRegisterRequest request, 
        Exception exception)
    {
        var error = new ModuleRegisterError
        {
            ModuleType = moduleType,
            ErrorMessage = $"Error in request {request.Key} (from {request.RequestFrom}): {exception.Message}",
            ErrorType = ModuleRegisterErrorType.ConfigurationError,
            GuideFrom = request.RequestFrom,
            Phase = request.RequestMethod
        };
        ModuleRegisterErrors.Add(error);
        
        // Immediately check if the module should be disabled due to exception
        CheckDisableModuleIfHasException(moduleType, error);
    }
    
    /// <summary>
    /// Checks if a module has DisableModuleIfHasException set and disables it if needed
    /// </summary>
    /// <param name="moduleType">The module type to check</param>
    /// <param name="error">The error that occurred</param>
    /// <returns>If it should disable module, return true.</returns>
    internal static bool CheckDisableModuleIfHasException(Type moduleType, ModuleRegisterError error)
    {
        // Try to find the module option through MoModuleRegisterCentre
        if (MoModuleRegisterCentre.TryGetModuleRequestInfo(moduleType, out var requestInfo))
        {
            // Check if the module has DisableModuleIfHasException set
            var moduleOption = requestInfo.ModuleOption;

            if (moduleOption.DisableModuleIfHasException ?? ModuleCoreOption.DisableModuleIfHasException)
            {
                // Disable the module
                if (ModuleManager.DisableModule(moduleType))
                {
                    // Log the error but don't throw an exception for this module
                    MoModuleRegisterCentre.Logger.LogWarning(
                        "Module {ModuleName} was disabled due to an error: {ErrorMessage}",
                        moduleType.Name,
                        error);
                }

                return true;
            }
        }

        return false;
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
                sb.Append(error);
                sb.AppendLine();
            }
        }
        
        return sb.ToString();
    }
    
   
    /// <summary>
    /// Raises an exception if there are any module registration errors.
    /// </summary>
    public static void RaiseModuleErrors()
    {
        if (ModuleRegisterErrors.Count == 0)
        {
            return;
        }

        // Filter out errors for modules that have already been disabled
        var errorsToThrow = ModuleRegisterErrors.Where(e => !ModuleManager.IsModuleDisabled(e.ModuleType)).ToList();
        
        //// Log summary of disabled modules
        //var disabledModules = ModuleManager.GetDisabledModuleTypes();
        //if (disabledModules.Count > 0)
        //{
        //    MoModuleRegisterCentre.Logger.LogWarning(
        //        "The following modules were disabled due to exceptions: {DisabledModules}",
        //        string.Join(", ", disabledModules.Select(m => m.Name)));
        //}

        // Throw exception if there are any remaining errors
        if (errorsToThrow.Count > 0)
        {
            throw new ModuleRegisterException(BuildErrorMessage(errorsToThrow));
        }
    }

} 