namespace Monica.Core.HostedService.Metrics;

/// <summary>
/// Defines meter and instrument names emitted by the HostedService module.
/// </summary>
public static class HostedServiceMetricNames
{
    /// <summary>
    /// Meter name used for Monica hosted-service metrics.
    /// </summary>
    public const string MeterName = "Monica.Core.HostedService";

    /// <summary>
    /// Counter instrument that records hosted-service state transitions.
    /// </summary>
    public const string Transitions = "monica.hostedservice.transitions";

    /// <summary>
    /// Observable up-down counter instrument that reports the current hosted-service state distribution.
    /// </summary>
    public const string State = "monica.hostedservice.state";
}
