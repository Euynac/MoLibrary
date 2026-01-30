namespace Monica.Framework.UI.UIObservableInstance.Models;

/// <summary>
/// Health state enumeration for observable instances
/// </summary>
public enum HealthState
{
    /// <summary>
    /// Unknown health state (no log level mapping configured)
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Healthy state (Debug or Information level)
    /// </summary>
    Healthy = 1,

    /// <summary>
    /// Unhealthy state (Warning, Error, or Critical level)
    /// </summary>
    Unhealthy = 2
}
