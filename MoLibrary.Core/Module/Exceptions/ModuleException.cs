using MoLibrary.Core.Module.ModuleAnalyser;
using System;

namespace MoLibrary.Core.Module.Exceptions;

/// <summary>
/// Base exception class for all module-related exceptions in MoLibrary.
/// </summary>
public class ModuleException(string message) : Exception(message)
{
    /// <summary>
    /// Module type where the exception occurred.
    /// </summary>
    public Type? ModuleType { get; set; }
    
    /// <summary>
    /// Module dependencies information when the exception occurred.
    /// </summary>
    public ModuleDependencyInfo? DependencyInfo { get; set; }
} 