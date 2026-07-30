namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>Defines profiler-owned system phase names that are not module lifecycle phases.</summary>
internal static class ModuleCompositionSystemPhaseNames
{
    /// <summary>The application callback passed to <c>AddMonica(...)</c>.</summary>
    internal const string APPLICATION_CONFIGURATION = "ApplicationConfiguration";
}
