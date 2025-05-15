using System;
using System.Collections.Generic;
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
    public Type ModuleType { get; set; } = null!;
    
    /// <summary>
    /// Description of the error that occurred.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// The module enum that was being guided from when the error occurred.
    /// </summary>
    public EMoModules? GuideFrom { get; set; }
    
    /// <summary>
    /// List of missing configuration method keys.
    /// </summary>
    public List<string> MissingConfigKeys { get; set; } = new();
    
    /// <summary>
    /// Information about module dependencies when the error occurred.
    /// </summary>
    public ModuleDependencyInfo? DependencyInfo { get; set; }
} 