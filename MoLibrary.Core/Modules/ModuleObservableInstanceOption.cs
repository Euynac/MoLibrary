using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Core.Modules;

/// <summary>
/// Configuration options for the ObservableInstance module
/// </summary>
public class ModuleObservableInstanceOption : MoModuleOption<ModuleObservableInstance>
{
    /// <summary>
    /// Default maximum history size
    /// </summary>
    public int DefaultMaxHistorySize { get; set; } = 100;
}
