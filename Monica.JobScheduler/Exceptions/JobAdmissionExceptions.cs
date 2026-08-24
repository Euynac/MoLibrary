namespace Monica.JobScheduler.Exceptions;

/// <summary>
/// Thrown when execution admission is temporarily closed while a newer desired catalog converges.
/// </summary>
public sealed class JobCatalogTransitionException(string schedulerScopeKey)
    : InvalidOperationException(
        $"Scheduler scope '{schedulerScopeKey}' is transitioning to a newer catalog release; " +
        "new execution admission is temporarily paused.");

/// <summary>
/// Thrown when an application caller's expected executable revision no longer matches the active catalog.
/// </summary>
public sealed class JobRevisionMismatchException(string jobKey)
    : InvalidOperationException(
        $"Active job '{jobKey}' no longer matches the executable revision expected by this caller.");
