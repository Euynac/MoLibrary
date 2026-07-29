namespace Monica.Core.Modularity.Models;

/// <summary>
/// Identifies the latest composition checkpoint by which scheduled module work must complete.
/// </summary>
/// <remarks>
/// A deadline is a deterministic composition barrier, not a timeout. Monica may start work before the selected
/// checkpoint, but the serial composition pipeline cannot cross that checkpoint until every due work item completes.
/// </remarks>
public enum ModuleCompositionWorkDeadline
{
    /// <summary>
    /// Work must complete before Monica begins business-type iteration.
    /// </summary>
    BeforeBusinessTypeIteration = 0,

    /// <summary>
    /// Work must complete before post-service configuration begins and may finish earlier.
    /// </summary>
    BeforePostConfigureServices = 1,

    /// <summary>
    /// Work must complete before <c>AddMonica(...)</c> returns and the host service provider can be built.
    /// </summary>
    BeforeServiceRegistrationCompletion = 2
}
