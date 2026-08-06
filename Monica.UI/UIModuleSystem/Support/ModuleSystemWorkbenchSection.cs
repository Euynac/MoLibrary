namespace Monica.UI.UIModuleSystem.Support;

/// <summary>
/// Identifies a stable, deep-linkable section of the module diagnostics workbench.
/// </summary>
public enum ModuleSystemWorkbenchSection
{
    /// <summary>Composition outcome, findings, hotspots, and critical path.</summary>
    Overview,

    /// <summary>Timeline, blocking causality, callbacks, and startup work.</summary>
    Performance,

    /// <summary>Searchable and faceted module catalog.</summary>
    Modules,

    /// <summary>Selected-module dependency neighborhood.</summary>
    Dependencies,

    /// <summary>Type-discovery stages, queries, and assembly inventory.</summary>
    Discovery
}

internal static class ModuleSystemWorkbenchSectionExtensions
{
    internal static string ToRouteSegment(this ModuleSystemWorkbenchSection section) => section switch
    {
        ModuleSystemWorkbenchSection.Overview => "overview",
        ModuleSystemWorkbenchSection.Performance => "performance",
        ModuleSystemWorkbenchSection.Modules => "modules",
        ModuleSystemWorkbenchSection.Dependencies => "dependencies",
        ModuleSystemWorkbenchSection.Discovery => "discovery",
        _ => "overview"
    };

    internal static ModuleSystemWorkbenchSection Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "performance" => ModuleSystemWorkbenchSection.Performance,
        "modules" => ModuleSystemWorkbenchSection.Modules,
        "dependencies" => ModuleSystemWorkbenchSection.Dependencies,
        "discovery" => ModuleSystemWorkbenchSection.Discovery,
        _ => ModuleSystemWorkbenchSection.Overview
    };
}

/// <summary>Describes the current snapshot request state without discarding previously loaded data.</summary>
public enum ModuleSystemWorkbenchLoadState
{
    /// <summary>No snapshot has completed.</summary>
    InitialLoading,

    /// <summary>A snapshot is available.</summary>
    Ready,

    /// <summary>A newer snapshot is being requested while the current snapshot remains visible.</summary>
    Refreshing,

    /// <summary>The initial request failed and no snapshot is available.</summary>
    Failed
}

/// <summary>Describes the independently loaded state of lazy diagnostic data.</summary>
public enum ModuleSystemLazyLoadState
{
    /// <summary>The view has not requested the data.</summary>
    NotRequested,

    /// <summary>The request is in progress.</summary>
    Loading,

    /// <summary>The data is available.</summary>
    Ready,

    /// <summary>The request failed.</summary>
    Failed
}

/// <summary>Filters the module catalog by participation state.</summary>
public enum ModuleCatalogStateFilter
{
    /// <summary>Show every declared module.</summary>
    All,

    /// <summary>Show only active modules.</summary>
    Active,

    /// <summary>Show only intentionally disabled modules.</summary>
    Disabled
}

/// <summary>Filters the module catalog by runtime capability.</summary>
public enum ModuleCatalogCapabilityFilter
{
    /// <summary>Show modules with any capability.</summary>
    All,

    /// <summary>Show UI modules.</summary>
    Ui,

    /// <summary>Show web modules.</summary>
    Web,

    /// <summary>Show modules that require a web host.</summary>
    WebHostRequired
}
