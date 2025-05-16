using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.ModuleAnalyser;

public class MoModuleAnalyser
{
    /// <summary>
    /// Dictionary mapping module types to their enum representations.
    /// </summary>
    public static Dictionary<Type, EMoModules> ModuleTypeToEnumMap { get; set; } = new();
}