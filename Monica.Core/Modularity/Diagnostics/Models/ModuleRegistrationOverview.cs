using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Module registration overview.
/// </summary>
public class ModuleRegistrationOverview
{
    /// <summary>
    /// Enabled modules.
    /// </summary>
    public List<ModuleBasicInfo> EnabledModules { get; set; } = [];

    /// <summary>
    /// Disabled modules.
    /// </summary>
    public List<ModuleBasicInfo> DisabledModules { get; set; } = [];

    /// <summary>
    /// Module registration order map keyed by order.
    /// </summary>
    public Dictionary<int, ModuleBasicInfo> ModulesByOrder { get; set; } = [];

    /// <summary>
    /// Registration statistics.
    /// </summary>
    public ModuleRegistrationStatistics Statistics { get; set; } = new();
}

/// <summary>
/// Basic module information.
/// </summary>
public class ModuleBasicInfo
{
    /// <summary>
    /// Module type name.
    /// </summary>
    public string ModuleTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Fully qualified module type name.
    /// </summary>
    public string ModuleFullTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Module key.
    /// </summary>
    public ModuleKey? ModuleKey { get; set; }

    /// <summary>
    /// Registration order.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Current module phase.
    /// </summary>
    public ModulePhase Status { get; set; }

    /// <summary>
    /// Direct dependencies.
    /// </summary>
    public List<ModuleKey> Dependencies { get; set; } = [];

    /// <summary>
    /// Initialization time in milliseconds.
    /// </summary>
    public long InitializationTimeMs { get; set; }

    /// <summary>
    /// Indicates whether the module is disabled.
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// Indicates whether the module participates in the ASP.NET Core lifecycle.
    /// </summary>
    public bool IsWebModule { get; set; }

    /// <summary>
    /// Indicates whether the module is currently running in downgraded non-web mode.
    /// </summary>
    public bool IsDowngradedFromWebModule { get; set; }

    /// <summary>
    /// Indicates whether registration errors exist.
    /// </summary>
    public bool HasErrors { get; set; }
}

/// <summary>
/// Module registration statistics.
/// </summary>
public class ModuleRegistrationStatistics
{
    /// <summary>
    /// Total module count.
    /// </summary>
    public int TotalModules { get; set; }

    /// <summary>
    /// Enabled module count.
    /// </summary>
    public int EnabledModules { get; set; }

    /// <summary>
    /// Disabled module count.
    /// </summary>
    public int DisabledModules { get; set; }

    /// <summary>
    /// Total initialization time in milliseconds.
    /// </summary>
    public long TotalInitializationTimeMs { get; set; }

    /// <summary>
    /// Five slowest modules.
    /// </summary>
    public List<ModuleBasicInfo> SlowestModules { get; set; } = [];
} 
