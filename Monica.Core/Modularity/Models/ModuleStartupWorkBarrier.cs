namespace Monica.Core.Modularity.Models;

/// <summary>
/// Identifies the host-startup barrier that governs scheduled module work.
/// </summary>
/// <remarks>
/// A barrier is an ordering boundary, not a timeout. Monica makes work eligible as soon as it is submitted and waits
/// only at the selected boundary. While submissions remain open, <see cref="NoBarrier"/> work cannot consume the lane
/// reserved for later required work; a single-lane scheduler therefore starts it after submissions close. It never
/// creates a host-readiness barrier, but remains owned and observed until it completes or the host is disposed.
/// </remarks>
public enum ModuleStartupWorkBarrier
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
    BeforeServiceRegistrationCompletion = 2,

    /// <summary>
    /// Work must complete before any Generic Host lifecycle participant starts.
    /// </summary>
    BeforeHostLifecycle = 3,

    /// <summary>
    /// Work does not block composition or host startup. Failures are diagnostic-only.
    /// </summary>
    NoBarrier = 4
}
