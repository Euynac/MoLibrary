using System.Text;
using MoLibrary.Core.Module.Features;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Exceptions;

/// <summary>
/// Represents an error that occurred during module registration.
/// </summary>
public class ModuleRegisterError
{
    /// <summary>
    /// The type of the module where the error occurred.
    /// </summary>
    public required Type ModuleType { get; set; }
    
    /// <summary>
    /// Description of the error that occurred.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// The type of error that occurred during module registration.
    /// </summary>
    public ModuleRegisterErrorType ErrorType { get; set; } = ModuleRegisterErrorType.General;
    
    /// <summary>
    /// The module enum that was being guided from when the error occurred.
    /// </summary>
    public EMoModules? GuideFrom { get; set; }
    
    /// <summary>
    /// The configuration phase where the error occurred.
    /// </summary>
    public EMoModuleConfigMethods? Phase { get; set; }
    
    /// <summary>
    /// List of missing configuration method keys.
    /// </summary>
    public List<string> MissingConfigKeys { get; set; } = new();
    
    /// <summary>
    /// Information about module dependencies when the error occurred.
    /// </summary>
    public ModuleDependencyInfo? DependencyInfo { get; set; }

    /// <summary>
    /// Returns a formatted string representation of the module registration error.
    /// </summary>
    /// <returns>A detailed formatted error message.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"  - Error Type: {ErrorType}");
        sb.AppendLine($"  - Details: {ErrorMessage}");
        
        if (GuideFrom.HasValue)
        {
            sb.AppendLine($"  - Source: {GuideFrom}");
        }
        
        if (Phase.HasValue)
        {
            sb.AppendLine($"  - Phase: {Phase}");
        }
        
        if (MissingConfigKeys.Count > 0)
        {
            sb.AppendLine("  - Missing configuration methods:");
            
            foreach (var key in MissingConfigKeys)
            {
                sb.AppendLine($"    * {key}");
            }
        }
        
        if (DependencyInfo != null)
        {
            sb.AppendLine("  - Dependency Information:");
            sb.AppendLine($"    {DependencyInfo}");
        }
        
        return sb.ToString();
    }
} 